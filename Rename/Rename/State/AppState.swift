import Foundation
import AppKit
import Combine
import SwiftUI

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
    @Published var errorMessage: String? = nil
    @Published var extensionFilter: String = ""

    private let maxUndoDepth = 10
    private var isRecomputing = false

    var canUndo: Bool { !undoStack.isEmpty }

    var conflictedNames: Set<String> {
        ConflictDetector.findConflicts(in: files.map(\.computedName))
    }

    var hasConflicts: Bool { !conflictedNames.isEmpty }

    // MARK: - File Loading

    func addFiles(from urls: [URL]) {
        let newFiles = FileLoader.makeFiles(from: urls, existing: files)
        files.append(contentsOf: newFiles)
        loadThumbnails(for: newFiles)
    }

    func addFilesFromFolder(_ folder: URL) {
        addFiles(from: FileLoader.topLevelFileURLs(in: folder))
    }

    func clearFiles() {
        files = []
        selectedFileID = nil
    }

    func openFolderPicker() {
        FileLoader.openFolderPanel { [weak self] urls in
            self?.addFiles(from: urls)
        }
    }

    func openFilePicker() {
        FileLoader.openFilePanel { [weak self] urls in
            self?.addFiles(from: urls)
        }
    }

    // MARK: - Ordering

    func moveFiles(from source: IndexSet, to destination: Int) {
        files.move(fromOffsets: source, toOffset: destination)
    }

    // MARK: - Rules

    func addRule(_ rule: RenameRule) {
        rules.append(RenameRuleItem(rule: rule))
    }

    func moveRules(from source: IndexSet, to destination: Int) {
        rules.move(fromOffsets: source, toOffset: destination)
    }

    func removeRule(id: UUID) {
        rules.removeAll { $0.id == id }
    }

    func toggleRule(id: UUID) {
        guard let index = rules.firstIndex(where: { $0.id == id }) else { return }
        rules[index].isEnabled.toggle()
    }

    // MARK: - Rename

    func applyRename() {
        guard !hasConflicts else { return }
        let changes = files.map { file -> RenameChange in
            let newURL = file.originalURL
                .deletingLastPathComponent()
                .appendingPathComponent(file.computedName)
            return RenameChange(from: file.originalURL, to: newURL)
        }
        do {
            let operation = try RenameExecutor.execute(changes: changes)
            pushUndo(operation)
            // Update originalURLs to the new paths, preserving cached thumbnails
            for (index, change) in changes.enumerated() {
                let existingThumbnail = files[index].thumbnail
                files[index] = RenameFile(originalURL: change.to)
                files[index].thumbnail = existingThumbnail
            }
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func performUndo() {
        guard let operation = popUndo() else { return }
        do {
            try RenameExecutor.undo(operation)
            // Reverse the URL updates
            let reversedURLs = Dictionary(
                uniqueKeysWithValues: operation.changes.map { ($0.to, $0.from) }
            )
            files = files.map { file in
                if let original = reversedURLs[file.originalURL] {
                    return RenameFile(originalURL: original)
                }
                return file
            }
            loadThumbnails(for: files)
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    // MARK: - Undo Stack

    func pushUndo(_ operation: RenameOperation) {
        undoStack.append(operation)
        if undoStack.count > maxUndoDepth {
            undoStack.removeFirst()
        }
    }

    func popUndo() -> RenameOperation? {
        undoStack.isEmpty ? nil : undoStack.removeLast()
    }

    // MARK: - Private

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

    private func loadThumbnails(for targets: [RenameFile]) {
        for file in targets {
            guard let index = files.firstIndex(where: { $0.id == file.id }) else { continue }
            Task {
                let image = await ThumbnailService.shared.thumbnail(for: file.originalURL)
                self.files[index].thumbnail = image
            }
        }
    }
}
