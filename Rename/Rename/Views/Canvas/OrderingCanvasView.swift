import SwiftUI

enum CanvasMode: String, CaseIterable {
    case grid = "Grid"
    case list = "List"
}

struct OrderingCanvasView: View {
    @EnvironmentObject var appState: AppState
    @State private var mode: CanvasMode = .grid

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
                    .draggable(file.id.uuidString)
                    .dropDestination(for: String.self) { items, _ in
                        guard let idString = items.first,
                              let fromID = UUID(uuidString: idString),
                              let fromIndex = appState.files.firstIndex(where: { $0.id == fromID }),
                              let toIndex = appState.files.firstIndex(where: { $0.id == file.id }),
                              fromIndex != toIndex else { return false }
                        withAnimation {
                            appState.files.move(
                                fromOffsets: IndexSet(integer: fromIndex),
                                toOffset: toIndex > fromIndex ? toIndex + 1 : toIndex
                            )
                        }
                        return true
                    }
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
            .onMove(perform: appState.extensionFilter.isEmpty ? { source, destination in
                appState.moveFiles(from: source, to: destination)
            } : nil)
        }
        .listStyle(.plain)
    }
}
