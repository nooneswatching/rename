import Foundation
import Combine

@MainActor
final class AppState: ObservableObject {
    @Published var files: [RenameFile] = [] {
        didSet { recomputeNames() }
    }
    @Published var rules: [RenameRuleItem] = [] {
        didSet { recomputeNames() }
    }
    @Published var undoStack: [RenameOperation] = []
    @Published var selectedFileID: UUID? = nil

    private let maxUndoDepth = 10

    var canUndo: Bool { !undoStack.isEmpty }

    func pushUndo(_ operation: RenameOperation) {
        undoStack.append(operation)
        if undoStack.count > maxUndoDepth {
            undoStack.removeFirst()
        }
    }

    func popUndo() -> RenameOperation? {
        undoStack.isEmpty ? nil : undoStack.removeLast()
    }

    private var isRecomputing = false

    private func recomputeNames() {
        guard !isRecomputing else { return }
        isRecomputing = true
        defer { isRecomputing = false }
        let names = RenameEngine.compute(files: files, rules: rules)
        var updated = files
        for (index, name) in zip(files.indices, names) {
            updated[index].computedName = name
        }
        files = updated
    }
}
