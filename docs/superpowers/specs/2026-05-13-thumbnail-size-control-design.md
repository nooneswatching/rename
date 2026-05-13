# Thumbnail Size Control — Design Spec

**Date:** 2026-05-13  
**Status:** Approved

---

## Overview

Add a continuous slider to the grid canvas header that lets the user resize thumbnails on the fly. The control is visible only in Grid mode and does not persist between launches.

---

## Scope

- Grid view only (List view thumbnails are unchanged)
- No persistence — resets to default on each launch
- Continuous slider, not discrete steps

---

## State

A single property on `OrderingCanvasView`:

```swift
@State private var thumbnailSize: CGFloat = 80
```

Range: `50...160`. Default: `80` (current fixed height). No other component owns this value.

---

## Header Bar

The trailing area of the 38pt canvas header — currently occupied by a hidden phantom button used for centering — becomes a size-control `HStack` when in Grid mode:

```
[photo icon (small)]  [slider ~100pt wide]  [photo icon (large)]
```

- Left icon: `Image(systemName: "photo")` at `.caption` font size
- Right icon: `Image(systemName: "photo")` at `.body` font size
- Slider: `Slider(value: $thumbnailSize, in: 50...160)`, `frame(width: 100)`
- In List mode: trailing area is empty (existing layout preserved)

The leading Remove button and centered Grid/List picker are untouched.

---

## `FileCardView` Scaling

Add a `thumbnailSize: CGFloat` parameter. All card dimensions derive from it:

| Property | Value |
|---|---|
| Thumbnail width | `thumbnailSize * 1.25` |
| Thumbnail height | `thumbnailSize` |
| Text label `frame(width:)` | `thumbnailSize * 1.25` |

The current 5:4 aspect ratio (100×80) is preserved at all sizes.

The grid column minimum width updates to `thumbnailSize * 1.25 + 20` so columns reflow live as the slider moves.

`thumbnailSize` is passed into the `FileCardView` call inside the `fileItem` `@ViewBuilder` helper in `OrderingCanvasView`. The helper's signature gains a `thumbnailSize: CGFloat` parameter so both `gridView` and the drag preview can supply the right value.

---

## Drag Preview

The drag ghost preview in `dragPreview(for:)` passes a fixed `thumbnailSize: 80` to `FileCardView` regardless of the current slider value. This prevents the ghost from looking unexpectedly large or small at slider extremes.

---

## Files Changed

| File | Change |
|---|---|
| `OrderingCanvasView.swift` | Add `thumbnailSize` state; update header to show slider in grid mode; pass `thumbnailSize` to `FileCardView` calls and `GridItem` minimum |
| `FileCardView.swift` | Add `thumbnailSize: CGFloat` parameter; replace hardcoded `100`/`80` dimensions with derived values |
| `FileRowView.swift` | No changes |
