import SwiftUI
import UniformTypeIdentifiers
import AppKit

enum CanvasMode: String, CaseIterable {
    case grid = "Grid"
    case list = "List"
}

enum DropIndicator: Equatable {
    case leading, trailing
}

struct OrderingCanvasView: View {
    @EnvironmentObject var appState: AppState
    @State private var mode: CanvasMode = .grid
    @State private var lastClickedID: UUID? = nil
    @State private var draggingIDs: Set<UUID> = []
    @State private var dropInsertIndex: Int? = nil
    @State private var thumbnailSize: CGFloat = 80
    @State private var cardFrames: [UUID: CGRect] = [:]
    @State private var selectionDragRect: CGRect? = nil

    private var visibleFiles: [RenameFile] {
        guard !appState.extensionFilter.isEmpty else { return appState.files }
        return appState.files.filter {
            $0.originalURL.pathExtension.lowercased() == appState.extensionFilter
        }
    }

    private var conflictNames: Set<String> { appState.conflictedNames }
    private var selectionCount: Int { appState.selectedFileIDs.count }

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                if selectionCount > 0 {
                    Button(action: removeSelected) {
                        Label("Remove \(selectionCount)", systemImage: "trash")
                            .foregroundStyle(.red)
                    }
                    .buttonStyle(.plain)
                    .padding(.leading, 4)
                }
                Spacer()
                Picker("View", selection: $mode) {
                    ForEach(CanvasMode.allCases, id: \.self) { m in
                        Text(m.rawValue).tag(m)
                    }
                }
                .pickerStyle(.segmented)
                .frame(width: 140)
                Spacer()
                if mode == .grid {
                    HStack(spacing: 4) {
                        Image(systemName: "photo")
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                        Slider(value: $thumbnailSize, in: 50...160)
                            .frame(width: 100)
                            .accessibilityLabel("Thumbnail size")
                        Image(systemName: "photo")
                            .font(.body)
                            .foregroundStyle(.secondary)
                        }
                    .padding(.trailing, 4)
                } else if selectionCount > 0 {
                    Button(action: {}) {
                        Label("Remove \(selectionCount)", systemImage: "trash")
                    }
                    .buttonStyle(.plain)
                    .padding(.trailing, 4)
                    .hidden()
                }
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
        .onKeyPress(.delete) {
            let hadSelection = selectionCount > 0
            if hadSelection { removeSelected() }
            return hadSelection ? .handled : .ignored
        }
    }

    // MARK: Selection

    private func handleTap(file: RenameFile) {
        let flags = NSEvent.modifierFlags
        let isCmd = flags.contains(.command)
        let isShift = flags.contains(.shift)

        if isCmd {
            if appState.selectedFileIDs.contains(file.id) {
                appState.selectedFileIDs.remove(file.id)
            } else {
                appState.selectedFileIDs.insert(file.id)
            }
            lastClickedID = file.id
        } else if isShift, let anchor = lastClickedID {
            let ids = visibleFiles.map(\.id)
            if let anchorIndex = ids.firstIndex(of: anchor),
               let targetIndex = ids.firstIndex(of: file.id) {
                let range = anchorIndex <= targetIndex
                    ? ids[anchorIndex...targetIndex]
                    : ids[targetIndex...anchorIndex]
                appState.selectedFileIDs.formUnion(range)
            } else {
                appState.selectedFileIDs = [file.id]
                lastClickedID = file.id
            }
        } else {
            appState.selectedFileIDs = [file.id]
            lastClickedID = file.id
        }
    }

    private func removeSelected() {
        let visibleSelected = Set(visibleFiles.map(\.id)).intersection(appState.selectedFileIDs)
        visibleSelected.forEach { cardFrames.removeValue(forKey: $0) }
        appState.removeFiles(ids: visibleSelected)
        lastClickedID = nil
    }

    // MARK: Drag helpers

    private func startDrag(for file: RenameFile) -> NSItemProvider {
        draggingIDs = []
        var ids = appState.selectedFileIDs
        ids.insert(file.id)
        draggingIDs = ids
        appState.selectedFileIDs = ids
        return NSItemProvider(object: ids.map(\.uuidString).joined(separator: ",") as NSString)
    }

    @ViewBuilder
    private func dragPreview(for file: RenameFile) -> some View {
        let count = appState.selectedFileIDs.count
        ZStack(alignment: .topTrailing) {
            FileCardView(
                file: file,
                isSelected: true,
                isConflict: false,
                isDragging: false,
                dropIndicator: nil,
                thumbnailSize: 80,
                onSelect: {}
            )
            if count > 1 {
                Text("\(count)")
                    .font(.caption2)
                    .fontWeight(.semibold)
                    .foregroundStyle(.white)
                    .padding(.horizontal, 6)
                    .padding(.vertical, 2)
                    .background(Color.accentColor, in: Capsule())
                    .offset(x: 6, y: -6)
            }
        }
    }

    // MARK: Drop indicator

    private func dropIndicator(at fileIndex: Int) -> DropIndicator? {
        guard let insertAt = dropInsertIndex else { return nil }
        if insertAt == fileIndex { return .leading }
        // Trailing indicator: show on the last visible card when appending past it
        let lastVisibleIndex = appState.files.firstIndex(where: { $0.id == visibleFiles.last?.id })
        if let lastVisibleIndex, insertAt == lastVisibleIndex + 1, fileIndex == lastVisibleIndex {
            return .trailing
        }
        return nil
    }

    // MARK: Per-item drag/drop wiring

    @ViewBuilder
    private func fileItem<Card: View>(
        file: RenameFile,
        splitValue: CGFloat,
        splitAxisIsX: Bool,
        @ViewBuilder card: (Int) -> Card
    ) -> some View {
        let fileIndex = appState.files.firstIndex(where: { $0.id == file.id }) ?? 0
        card(fileIndex)
            .contextMenu { contextMenu(for: file) }
            .onDrag { startDrag(for: file) } preview: { dragPreview(for: file) }
            .onDrop(of: [UTType.plainText], delegate: ReorderDropDelegate(
                targetID: file.id,
                targetIndex: fileIndex,
                splitValue: splitValue,
                splitAxisIsX: splitAxisIsX,
                appState: appState,
                draggingIDs: $draggingIDs,
                dropInsertIndex: $dropInsertIndex
            ))
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

    // MARK: Context menu

    @ViewBuilder
    private func contextMenu(for file: RenameFile) -> some View {
        let targetIDs = appState.selectedFileIDs.contains(file.id)
            ? appState.selectedFileIDs
            : [file.id]
        let label = targetIDs.count > 1 ? "Remove \(targetIDs.count) Files" : "Remove from List"

        Button(role: .destructive, action: {
            targetIDs.forEach { cardFrames.removeValue(forKey: $0) }
            appState.removeFiles(ids: targetIDs)
        }) {
            Label(label, systemImage: "trash")
        }
    }

    // MARK: Grid view

    private var gridView: some View {
        ScrollView {
            LazyVGrid(columns: [GridItem(.adaptive(minimum: thumbnailSize * 1.25 + 20))], spacing: 12) {
                ForEach(visibleFiles) { file in
                    fileItem(file: file, splitValue: thumbnailSize * 0.625 + 4, splitAxisIsX: true) { fileIndex in  // thumbnailWidth/2 + card padding
                        FileCardView(
                            file: file,
                            isSelected: appState.selectedFileIDs.contains(file.id),
                            isConflict: conflictNames.contains(file.computedName),
                            isDragging: draggingIDs.contains(file.id),
                            dropIndicator: dropIndicator(at: fileIndex),
                            thumbnailSize: thumbnailSize,
                            onSelect: { handleTap(file: file) }
                        )
                        .onGeometryChange(for: CGRect.self) { proxy in
                            proxy.frame(in: .named("gridCoordSpace"))
                        } action: { frame in
                            cardFrames[file.id] = frame
                        }
                    }
                }
            }
            .padding()
            .animation(.default, value: appState.files.map(\.id))
            .background {
                Color.clear
                    .contentShape(Rectangle())
                    .onTapGesture { appState.selectedFileIDs = [] }
                    .gesture(
                        DragGesture(minimumDistance: 5, coordinateSpace: .named("gridCoordSpace"))
                            .onChanged { value in
                                guard draggingIDs.isEmpty else { return }
                                let rect = CGRect(
                                    x: min(value.startLocation.x, value.location.x),
                                    y: min(value.startLocation.y, value.location.y),
                                    width: abs(value.location.x - value.startLocation.x),
                                    height: abs(value.location.y - value.startLocation.y)
                                )
                                selectionDragRect = rect
                                let visibleIDs = Set(visibleFiles.map(\.id))
                                appState.selectedFileIDs = Set(
                                    cardFrames
                                        .filter { visibleIDs.contains($0.key) && $0.value.intersects(rect) }
                                        .map(\.key)
                                )
                            }
                            .onEnded { _ in
                                selectionDragRect = nil
                            }
                    )
            }
            .coordinateSpace(name: "gridCoordSpace")
            .overlay(alignment: .topLeading) {
                if let rect = selectionDragRect {
                    Rectangle()
                        .fill(Color.accentColor.opacity(0.15))
                        .overlay { Rectangle().stroke(Color.accentColor.opacity(0.5), lineWidth: 1) }
                        .frame(width: rect.width, height: rect.height)
                        .offset(x: rect.minX, y: rect.minY)
                        .allowsHitTesting(false)
                }
            }
        }
    }

    // MARK: List view

    private var listView: some View {
        ScrollView {
            LazyVStack(spacing: 2) {
                ForEach(visibleFiles) { file in
                    fileItem(file: file, splitValue: 14, splitAxisIsX: false) { fileIndex in
                        FileRowView(
                            file: file,
                            isSelected: appState.selectedFileIDs.contains(file.id),
                            isConflict: conflictNames.contains(file.computedName),
                            isDragging: draggingIDs.contains(file.id),
                            dropIndicator: dropIndicator(at: fileIndex),
                            onSelect: { handleTap(file: file) }
                        )
                    }
                }
            }
            .padding(.vertical, 4)
        }
    }
}

// MARK: - Reorder Drop Delegate

struct ReorderDropDelegate: DropDelegate {
    let targetID: UUID
    let targetIndex: Int
    let splitValue: CGFloat
    let splitAxisIsX: Bool
    let appState: AppState
    var draggingIDs: Binding<Set<UUID>>
    var dropInsertIndex: Binding<Int?>

    func validateDrop(info: DropInfo) -> Bool {
        !info.itemProviders(for: [UTType.plainText]).isEmpty &&
        !draggingIDs.wrappedValue.contains(targetID)
    }

    func dropUpdated(info: DropInfo) -> DropProposal? {
        guard !draggingIDs.wrappedValue.isEmpty,
              !draggingIDs.wrappedValue.contains(targetID) else {
            return DropProposal(operation: .cancel)
        }
        let loc = info.location
        let insertBefore = splitAxisIsX ? loc.x < splitValue : loc.y < splitValue
        dropInsertIndex.wrappedValue = insertBefore ? targetIndex : targetIndex + 1
        return DropProposal(operation: .move)
    }

    func dropExited(info: DropInfo) {
        dropInsertIndex.wrappedValue = nil
    }

    func performDrop(info: DropInfo) -> Bool {
        let insertAt = dropInsertIndex.wrappedValue
        dropInsertIndex.wrappedValue = nil
        draggingIDs.wrappedValue = []

        guard let provider = info.itemProviders(for: [UTType.plainText]).first else { return false }
        provider.loadObject(ofClass: NSString.self) { object, _ in
            guard let idString = object as? String else { return }
            let ids = idString.split(separator: ",").compactMap { UUID(uuidString: String($0)) }
            guard !ids.isEmpty else { return }
            DispatchQueue.main.async {
                let fromOffsets = IndexSet(ids.compactMap { id in
                    appState.files.firstIndex(where: { $0.id == id })
                })
                guard !fromOffsets.isEmpty else { return }
                let toOffset = min(insertAt ?? {
                    guard let targetIdx = appState.files.firstIndex(where: { $0.id == targetID }) else { return 0 }
                    return (fromOffsets.min() ?? 0) < targetIdx ? targetIdx + 1 : targetIdx
                }(), appState.files.count)
                withAnimation {
                    appState.moveFiles(from: fromOffsets, to: toOffset)
                }
            }
        }
        return true
    }
}
