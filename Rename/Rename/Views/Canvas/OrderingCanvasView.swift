import SwiftUI
import UniformTypeIdentifiers

enum CanvasMode: String, CaseIterable {
    case grid = "Grid"
    case list = "List"
}

struct OrderingCanvasView: View {
    @EnvironmentObject var appState: AppState
    @State private var mode: CanvasMode = .grid
    @State private var draggingID: UUID? = nil

    private var visibleFiles: [RenameFile] {
        guard !appState.extensionFilter.isEmpty else { return appState.files }
        return appState.files.filter {
            $0.originalURL.pathExtension.lowercased() == appState.extensionFilter
        }
    }

    private var conflictNames: Set<String> { appState.conflictedNames }

    var body: some View {
        VStack(spacing: 0) {
            Picker("View", selection: $mode) {
                ForEach(CanvasMode.allCases, id: \.self) { m in
                    Text(m.rawValue).tag(m)
                }
            }
            .pickerStyle(.segmented)
            .frame(width: 140)
            .padding(8)

            Divider()

            if visibleFiles.isEmpty {
                emptyState
            } else if mode == .grid {
                gridView
            } else {
                listView
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    // MARK: Empty state

    private var emptyState: some View {
        VStack(spacing: 12) {
            Image(systemName: "photo.stack")
                .font(.system(size: 48))
                .foregroundStyle(.tertiary)
            Text("Drop files here or use the toolbar to load files")
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    // MARK: Grid view

    private var gridView: some View {
        ScrollView {
            LazyVGrid(columns: [GridItem(.adaptive(minimum: 120))], spacing: 12) {
                ForEach(visibleFiles) { file in
                    FileCardView(
                        file: file,
                        isSelected: appState.selectedFileID == file.id,
                        isConflict: conflictNames.contains(file.computedName),
                        onSelect: { appState.selectedFileID = file.id }
                    )
                    .onDrag {
                        draggingID = file.id
                        return NSItemProvider(object: file.id.uuidString as NSString)
                    }
                    .onDrop(of: [UTType.plainText], delegate: GridDropDelegate(
                        targetFile: file,
                        appState: appState,
                        draggingID: $draggingID
                    ))
                }
            }
            .padding()
            .animation(.default, value: appState.files.map(\.id))
        }
    }

    // MARK: List view

    private var listView: some View {
        List {
            ForEach(visibleFiles) { file in
                FileRowView(
                    file: file,
                    isSelected: appState.selectedFileID == file.id,
                    isConflict: conflictNames.contains(file.computedName),
                    onSelect: { appState.selectedFileID = file.id }
                )
            }
            .onMove { source, destination in
                appState.moveFiles(from: source, to: destination)
            }
        }
        .listStyle(.plain)
    }
}

// MARK: - Grid Drop Delegate

struct GridDropDelegate: DropDelegate {
    let targetFile: RenameFile
    let appState: AppState
    @Binding var draggingID: UUID?

    func performDrop(info: DropInfo) -> Bool {
        guard let draggingID,
              let fromIndex = appState.files.firstIndex(where: { $0.id == draggingID }),
              let toIndex = appState.files.firstIndex(where: { $0.id == targetFile.id }),
              fromIndex != toIndex else { return false }

        withAnimation {
            appState.files.move(
                fromOffsets: IndexSet(integer: fromIndex),
                toOffset: toIndex > fromIndex ? toIndex + 1 : toIndex
            )
        }
        self.draggingID = nil
        return true
    }

    func dropUpdated(info: DropInfo) -> DropProposal? {
        DropProposal(operation: .move)
    }

    func validateDrop(info: DropInfo) -> Bool { true }
}
