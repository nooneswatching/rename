import SwiftUI
import UniformTypeIdentifiers

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
            HStack {
                Spacer()
                Picker("View", selection: $mode) {
                    ForEach(CanvasMode.allCases, id: \.self) { m in
                        Text(m.rawValue).tag(m)
                    }
                }
                .pickerStyle(.segmented)
                .frame(width: 140)
                Spacer()
            }
            .frame(height: 38)
            .padding(.horizontal, 12)

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
                .font(.system(size: 56))
                .foregroundStyle(.tertiary)
            Text("Drop files here or use the toolbar to load files")
                .font(.body)
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
                        NSItemProvider(object: file.id.uuidString as NSString)
                    }
                    .onDrop(of: [UTType.plainText], delegate: GridDropDelegate(
                        targetID: file.id,
                        appState: appState
                    ))
                }
            }
            .padding()
            .animation(.default, value: appState.files.map(\.id))
        }
    }

    // MARK: List view

    private var listView: some View {
        ScrollView {
            LazyVStack(spacing: 2) {
                ForEach(visibleFiles) { file in
                    FileRowView(
                        file: file,
                        isSelected: appState.selectedFileID == file.id,
                        isConflict: conflictNames.contains(file.computedName),
                        onSelect: { appState.selectedFileID = file.id }
                    )
                    .onDrag {
                        NSItemProvider(object: file.id.uuidString as NSString)
                    }
                    .onDrop(of: [UTType.plainText], delegate: GridDropDelegate(
                        targetID: file.id,
                        appState: appState
                    ))
                }
            }
            .padding(.vertical, 4)
        }
    }
}

// MARK: - Grid Drop Delegate

struct GridDropDelegate: DropDelegate {
    let targetID: UUID
    let appState: AppState

    func performDrop(info: DropInfo) -> Bool {
        guard let provider = info.itemProviders(for: [UTType.plainText]).first else { return false }
        provider.loadObject(ofClass: NSString.self) { object, _ in
            guard let idString = object as? String,
                  let fromID = UUID(uuidString: idString) else { return }
            DispatchQueue.main.async {
                guard let fromIndex = appState.files.firstIndex(where: { $0.id == fromID }),
                      let toIndex = appState.files.firstIndex(where: { $0.id == targetID }),
                      fromIndex != toIndex else { return }
                withAnimation {
                    appState.files.move(
                        fromOffsets: IndexSet(integer: fromIndex),
                        toOffset: toIndex > fromIndex ? toIndex + 1 : toIndex
                    )
                }
            }
        }
        return true
    }

    func dropUpdated(info: DropInfo) -> DropProposal? {
        DropProposal(operation: .move)
    }

    func validateDrop(info: DropInfo) -> Bool { true }
}
