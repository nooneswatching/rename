# Canvas Selection Improvements — Design Spec

**Date:** 2026-05-13  
**Status:** Approved

---

## Overview

Two selection improvements to the grid canvas that make it feel native:

1. **Void-click deselect** — clicking empty space clears the selection
2. **Rubber-band selection** — dragging on empty space draws a rectangle and selects all cards it intersects (grid view only)

---

## Scope

- Grid view only for rubber-band (list view drag conflicts with scrolling)
- Void-click applies to grid view background
- No changes to existing cmd-click, shift-click, or delete behavior
- No persistence

---

## Files Changed

| File | Change |
|---|---|
| `Rename/Rename/Views/Canvas/OrderingCanvasView.swift` | All changes — new state, frame tracking, gesture, overlay |

No other files need modification.

---

## Feature 1: Void-Click Deselect

Add `.onTapGesture { appState.selectedFileIDs = [] }` to the grid's background layer. SwiftUI hit-testing ensures this fires only when the user taps empty space — card taps are consumed by the cards' own `onTapGesture` and do not propagate to the background.

No changes to `handleTap(file:)` or any card code.

---

## Feature 2: Rubber-Band Selection

### New State

```swift
@State private var cardFrames: [UUID: CGRect] = [:]
@State private var selectionDragRect: CGRect? = nil
```

`cardFrames` accumulates each visible card's frame in a shared coordinate space. `selectionDragRect` holds the live rectangle during a drag; `nil` when no drag is in progress.

### Coordinate Space

A named coordinate space `"gridCoordSpace"` is applied to the `LazyVGrid`'s content container. Both the card frame reporter and the drag gesture use this same space, ensuring coordinates are consistent regardless of scroll position.

### Card Frame Tracking

Each `FileCardView` in the grid gets:

```swift
.onGeometryChange(for: CGRect.self) { proxy in
    proxy.frame(in: .named("gridCoordSpace"))
} action: { frame in
    cardFrames[file.id] = frame
}
```

This updates automatically whenever the card's frame changes (thumbnail size slider, window resize, grid reflow).

### Drag Gesture

A `DragGesture(minimumDistance: 5, coordinateSpace: .named("gridCoordSpace"))` on the grid background:

**`onChanged`:**
```swift
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
```

**`onEnded`:** `selectionDragRect = nil` — selection is kept, rectangle disappears.

The `minimumDistance: 5` prevents accidental rubber-band triggers from tap-and-hold motions. Filtering by `visibleIDs` prevents selecting cards that have been removed or filtered out but still have stale entries in `cardFrames`.

### Background Layer

Both the void-click tap and the rubber-band drag gesture are attached to the `LazyVGrid`'s `.background` modifier (a `Color.clear` with `.contentShape(Rectangle())`). This ensures:
- Neither gesture fires when interacting with a card (cards consume their own events)
- The entire grid area including gaps between cards is interactive

### Visual Overlay

A non-interactive overlay rectangle rendered during drag:

```swift
Rectangle()
    .fill(Color.accentColor.opacity(0.15))
    .overlay { Rectangle().stroke(Color.accentColor.opacity(0.5), lineWidth: 1) }
    .frame(width: rect.width, height: rect.height)
    .offset(x: rect.minX, y: rect.minY)
    .allowsHitTesting(false)
```

Positioned in the `.overlay(alignment: .topLeading)` of the grid container. No corner radius — matches Finder's sharp rectangle style.
