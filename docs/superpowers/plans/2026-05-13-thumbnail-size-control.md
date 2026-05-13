# Thumbnail Size Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Finder-style continuous slider to the grid canvas header that lets the user resize thumbnail cards from 50–160pt height, grid-only, no persistence.

**Architecture:** A single `thumbnailSize: CGFloat` state on `OrderingCanvasView` drives all dimensions. `FileCardView` derives its thumbnail frame and text label width from a new `thumbnailSize` parameter. The header trailing area shows the slider in grid mode. No new files needed.

**Tech Stack:** SwiftUI, macOS 26, existing project patterns.

---

### Task 1: Scale `FileCardView` from a `thumbnailSize` parameter

**Files:**
- Modify: `Rename/Rename/Views/Canvas/FileCardView.swift`

- [ ] **Step 1: Add `thumbnailSize` parameter and a computed `thumbnailWidth` property**

Replace the current struct opening in `FileCardView.swift`:

```swift
struct FileCardView: View {
    let file: RenameFile
    let isSelected: Bool
    let isConflict: Bool
    let isDragging: Bool
    let dropIndicator: DropIndicator?
    let thumbnailSize: CGFloat
    let onSelect: () -> Void

    private var thumbnailWidth: CGFloat { thumbnailSize * 1.25 }
```

- [ ] **Step 2: Replace all hardcoded `100` / `80` dimensions in the view body**

Replace the full `body` property with the scaled version:

```swift
var body: some View {
    VStack(spacing: 4) {
        ZStack {
            RoundedRectangle(cornerRadius: 8)
                .fill(Color(NSColor.controlBackgroundColor))
                .frame(width: thumbnailWidth, height: thumbnailSize)

            if let thumbnail = file.thumbnail {
                Image(nsImage: thumbnail)
                    .resizable()
                    .aspectRatio(contentMode: .fit)
                    .frame(width: thumbnailWidth, height: thumbnailSize)
                    .clipShape(RoundedRectangle(cornerRadius: 8))
            } else {
                ProgressView()
                    .frame(width: thumbnailWidth, height: thumbnailSize)
            }
        }
        .overlay(
            RoundedRectangle(cornerRadius: 8)
                .stroke(isSelected ? Color.accentColor : Color.clear, lineWidth: 2)
        )

        Text(file.originalURL.lastPathComponent)
            .font(.caption2)
            .lineLimit(2)
            .truncationMode(.middle)
            .frame(width: thumbnailWidth)
            .foregroundStyle(.secondary)

        Text(file.computedName)
            .font(.caption2)
            .lineLimit(2)
            .truncationMode(.middle)
            .frame(width: thumbnailWidth)
            .foregroundStyle(isConflict ? .red : .accentColor)
    }
    .padding(4)
    .background(
        RoundedRectangle(cornerRadius: 10)
            .fill(isSelected ? Color.accentColor.opacity(0.08) : Color.clear)
    )
    .opacity(isDragging ? 0.4 : 1.0)
    .overlay(alignment: .leading) {
        if dropIndicator == .leading {
            Capsule()
                .fill(Color.accentColor)
                .frame(width: 2)
                .padding(.vertical, 4)
        }
    }
    .overlay(alignment: .trailing) {
        if dropIndicator == .trailing {
            Capsule()
                .fill(Color.accentColor)
                .frame(width: 2)
                .padding(.vertical, 4)
        }
    }
    .onTapGesture { onSelect() }
}
```

- [ ] **Step 3: Build to confirm `FileCardView` compiles (callers will fail — that's expected)**

```bash
cd /Users/joshuanewton/Library/CloudStorage/Dropbox/Development/Rename/Rename
xcodebuild -scheme Rename -configuration Debug build 2>&1 | grep -E "error:|BUILD"
```

Expected: build errors at the `FileCardView(...)` call sites in `OrderingCanvasView.swift` complaining about missing `thumbnailSize` argument. That's correct — Task 2 fixes them.

---

### Task 2: Add `thumbnailSize` state, slider, and wire through `OrderingCanvasView`

**Files:**
- Modify: `Rename/Rename/Views/Canvas/OrderingCanvasView.swift`

- [ ] **Step 1: Add `thumbnailSize` state variable**

In `OrderingCanvasView`, add below the existing `@State` declarations:

```swift
@State private var thumbnailSize: CGFloat = 80
```

Full state block after the change:
```swift
@State private var mode: CanvasMode = .grid
@State private var lastClickedID: UUID? = nil
@State private var draggingIDs: Set<UUID> = []
@State private var dropInsertIndex: Int? = nil
@State private var thumbnailSize: CGFloat = 80
```

- [ ] **Step 2: Replace the trailing area of the header bar**

Find and replace the trailing phantom-button block:

```swift
// BEFORE — remove this entire block:
if selectionCount > 0 {
    Button(action: {}) {
        Label("Remove \(selectionCount)", systemImage: "trash")
    }
    .buttonStyle(.plain)
    .padding(.trailing, 4)
    .hidden()
}
```

```swift
// AFTER — replace with:
if mode == .grid {
    HStack(spacing: 4) {
        Image(systemName: "photo")
            .font(.caption2)
            .foregroundStyle(.secondary)
        Slider(value: $thumbnailSize, in: 50...160)
            .frame(width: 100)
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
```

- [ ] **Step 3: Update the grid column minimum width to track `thumbnailSize`**

Find and replace the `LazyVGrid` line:

```swift
// BEFORE:
LazyVGrid(columns: [GridItem(.adaptive(minimum: 120))], spacing: 12) {

// AFTER:
LazyVGrid(columns: [GridItem(.adaptive(minimum: thumbnailSize * 1.25 + 20))], spacing: 12) {
```

- [ ] **Step 4: Update the `fileItem` call in `gridView` to pass `thumbnailSize` and a dynamic `splitValue`**

Find the `fileItem` call inside `gridView` and update:

```swift
// BEFORE:
fileItem(file: file, splitValue: 54, splitAxisIsX: true) { fileIndex in
    FileCardView(
        file: file,
        isSelected: appState.selectedFileIDs.contains(file.id),
        isConflict: conflictNames.contains(file.computedName),
        isDragging: draggingIDs.contains(file.id),
        dropIndicator: dropIndicator(at: fileIndex),
        onSelect: { handleTap(file: file) }
    )
}

// AFTER:
fileItem(file: file, splitValue: thumbnailSize * 0.625, splitAxisIsX: true) { fileIndex in
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
```

Note: `splitValue: thumbnailSize * 0.625` is `thumbnailWidth / 2` — the left/right midpoint of the card at any size, keeping the drop indicator split accurate.

- [ ] **Step 5: Update `dragPreview` to pass a fixed `thumbnailSize: 80`**

Find and update the `FileCardView` call inside `dragPreview(for:)`:

```swift
// BEFORE:
FileCardView(
    file: file,
    isSelected: true,
    isConflict: false,
    isDragging: false,
    dropIndicator: nil,
    onSelect: {}
)

// AFTER:
FileCardView(
    file: file,
    isSelected: true,
    isConflict: false,
    isDragging: false,
    dropIndicator: nil,
    thumbnailSize: 80,
    onSelect: {}
)
```

- [ ] **Step 6: Build and confirm clean compile**

```bash
cd /Users/joshuanewton/Library/CloudStorage/Dropbox/Development/Rename/Rename
xcodebuild -scheme Rename -configuration Debug build 2>&1 | tail -3
```

Expected output:
```
** BUILD SUCCEEDED **
```

- [ ] **Step 7: Commit**

```bash
cd /Users/joshuanewton/Library/CloudStorage/Dropbox/Development/Rename
git add Rename/Rename/Views/Canvas/FileCardView.swift \
        Rename/Rename/Views/Canvas/OrderingCanvasView.swift
git commit -m "feat: add thumbnail size slider to grid canvas header"
```
