# Canvas Selection Improvements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add void-click-to-deselect and Finder-style rubber-band selection to the grid canvas.

**Architecture:** Both features are pure additions to `OrderingCanvasView.swift`. A `.background` layer on the `LazyVGrid` handles void-click and rubber-band gestures — cards in the foreground consume their own events so the background only activates on empty space. Card frames are tracked via `.onGeometryChange` into a `[UUID: CGRect]` dictionary and continuously intersected against the drag rectangle to update `selectedFileIDs` live.

**Tech Stack:** SwiftUI (macOS 26), `DragGesture`, `.onGeometryChange`, named coordinate spaces.

---

### Task 1: Void-click deselect

**Files:**
- Modify: `Rename/Rename/Views/Canvas/OrderingCanvasView.swift`

This task adds a single tap gesture to the grid background. Tapping a card still calls `handleTap(file:)` as before — SwiftUI hit-testing ensures card taps don't propagate to the background. Tapping empty space clears the selection.

- [ ] **Step 1: Add `.background` with void-click tap to `gridView`**

Find the current `gridView` computed property:

```swift
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
                }
            }
        }
        .padding()
        .animation(.default, value: appState.files.map(\.id))
    }
}
```

Replace it with:

```swift
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
                }
            }
        }
        .padding()
        .animation(.default, value: appState.files.map(\.id))
        .background {
            Color.clear
                .contentShape(Rectangle())
                .onTapGesture { appState.selectedFileIDs = [] }
        }
    }
}
```

- [ ] **Step 2: Build and confirm clean compile**

```bash
cd /Users/joshuanewton/Library/CloudStorage/Dropbox/Development/Rename/Rename
xcodebuild -scheme Rename -configuration Debug build 2>&1 | tail -3
```

Expected:
```
** BUILD SUCCEEDED **
```

- [ ] **Step 3: Commit**

```bash
cd /Users/joshuanewton/Library/CloudStorage/Dropbox/Development/Rename
git add Rename/Rename/Views/Canvas/OrderingCanvasView.swift
git commit -m "feat: deselect all when clicking empty canvas space"
```

---

### Task 2: Rubber-band (marquee) selection

**Files:**
- Modify: `Rename/Rename/Views/Canvas/OrderingCanvasView.swift`

This task adds the rubber-band gesture. Card frames are reported into a named coordinate space; the drag gesture intersects the live rectangle against those frames to update `selectedFileIDs` continuously. A semi-transparent accent rectangle is overlaid during drag.

- [ ] **Step 1: Add two new state variables**

In `OrderingCanvasView`, find the existing `@State` block:

```swift
@State private var draggingIDs: Set<UUID> = []
@State private var dropInsertIndex: Int? = nil
@State private var thumbnailSize: CGFloat = 80
```

Replace with:

```swift
@State private var draggingIDs: Set<UUID> = []
@State private var dropInsertIndex: Int? = nil
@State private var thumbnailSize: CGFloat = 80
@State private var cardFrames: [UUID: CGRect] = [:]
@State private var selectionDragRect: CGRect? = nil
```

- [ ] **Step 2: Replace `gridView` with the final version**

Find and replace the entire `gridView` computed property. This adds `.onGeometryChange` on each card, expands the background to include the `DragGesture`, attaches the named coordinate space, and adds the selection rectangle overlay:

```swift
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
                            // Guard: if drag started over a card, don't rubber-band
                            let startedOnCard = cardFrames.values.contains { $0.contains(value.startLocation) }
                            guard !startedOnCard else {
                                selectionDragRect = nil
                                return
                            }
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
```

- [ ] **Step 3: Build and confirm clean compile**

```bash
cd /Users/joshuanewton/Library/CloudStorage/Dropbox/Development/Rename/Rename
xcodebuild -scheme Rename -configuration Debug build 2>&1 | tail -3
```

Expected:
```
** BUILD SUCCEEDED **
```

- [ ] **Step 4: Commit**

```bash
cd /Users/joshuanewton/Library/CloudStorage/Dropbox/Development/Rename
git add Rename/Rename/Views/Canvas/OrderingCanvasView.swift
git commit -m "feat: add rubber-band selection to grid canvas"
```
