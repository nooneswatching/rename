# Rename App — Design Spec

**Date:** 2026-05-11
**Platform:** macOS (native)
**Stack:** Swift + SwiftUI
**Distribution:** Local use, potentially shareable

---

## Overview

A native macOS batch file renaming app with a signature feature: a visual ordering canvas where users can drag files into any order and then number/rename them in that exact sequence. The app targets mixed-file workflows (images, documents, video, audio) and is built to the standard of a distributable app.

---

## File Loading

Files are loaded into the app via any of four methods:

1. **Open Folder** — toolbar button opens a folder picker; all files in the selected folder are loaded (top-level only, no deep recursion by default)
2. **Add Files** — toolbar button opens a file picker with multi-select enabled
3. **Drag files onto the window** — individual files or a folder dragged onto the app window are accepted
4. **Drag a folder onto the window** — loads all top-level files in the dropped folder

All four methods append to the current file list. Duplicate files (same URL) are silently ignored — loading the same file twice results in one entry. A "Clear All" button in the sidebar resets the list.

The sidebar shows the source directory path (or "Multiple sources" if files came from different places), a file count, and a filter control to show/hide files by extension.

---

## UI Layout

Single window, four regions:

```
┌─────────────────────────────────────────────────────────────┐
│  Toolbar: [Open Folder] [Add Files] [Grid|List toggle] [Undo]│
├──────────────┬──────────────────────────┬────────────────────┤
│              │                          │                    │
│   Sidebar    │    Ordering Canvas       │   Rules Panel      │
│              │                          │                    │
│ • Source dir │  Files displayed as      │ • Prefix / Suffix  │
│ • File count │  grid or list.           │ • Find & Replace   │
│ • Filter by  │  Drag to reorder.        │ • Numbering        │
│   extension  │  New name shown          │ • Case change      │
│              │  beneath each file.      │ • Insert/Remove    │
│ [Clear All]  │                          │ • Extension        │
│              │  Click any file to       │ • Date-based       │
│              │  select/deselect.        │ • Regex            │
│              │                          │                    │
│              │                          │ Rules are stacked  │
│              │                          │ top-to-bottom and  │
│              │                          │ can be reordered   │
│              │                          │ and toggled on/off │
├──────────────┴──────────────────────────┴────────────────────┤
│  Preview bar: [Before → After] for selected file  [Apply]    │
└─────────────────────────────────────────────────────────────┘
```

### Ordering Canvas — Grid Mode

Files are shown as cards in a wrapping grid. Each card contains:
- A thumbnail (actual image preview for images; system icon for other file types)
- The original filename below the thumbnail
- The computed new name beneath that, in a distinct color (e.g. blue)

Cards are draggable to reorder. The visual order of cards directly determines the numbering sequence when a numbering rule is active.

### Ordering Canvas — List Mode

Each row contains: drag handle, file icon, original name, arrow (→), computed new name. Rows are draggable to reorder.

Toggle between grid and list via a segmented control in the toolbar.

### Rules Panel

Rules are stacked vertically as collapsible cards. Each rule has:
- An enable/disable toggle
- A title (e.g. "Numbering", "Prefix")
- Inline configuration controls

Rules are applied top-to-bottom. Users can drag-reorder rules, which changes the application order. Rules supported:

| Rule | Configuration |
|------|---------------|
| Prefix | Text to prepend |
| Suffix | Text to append (before extension) |
| Find & Replace | Find string, replace string, case-sensitive toggle |
| Regex Find & Replace | Regex pattern, replacement string (with capture groups) |
| Numbering | Start number, step, digit count (zero-padded), position (prefix/suffix/replace) |
| Change Case | Options: lowercase, UPPERCASE, Title Case, camelCase |
| Insert at Position | Text to insert, character index |
| Remove Range | Start index, character count |
| Change Extension | New extension string |
| Date-based Naming | Date format string, date source (file modified date, file created date, today) |

### Bottom Preview Bar

Shows the full original → computed filename for the currently selected file. If no file is selected, shows the first file in the list. The "Apply" button opens the before/after review sheet.

### Before/After Review Sheet

A modal sheet with two columns: original name and computed new name for every file. Conflicts (two files resolving to the same name) are highlighted in red. The "Confirm" button is disabled when any conflicts exist. "Cancel" dismisses without changes.

---

## Data Model

```swift
struct RenameFile: Identifiable {
    let id: UUID
    let originalURL: URL
    var computedName: String      // output of RenameEngine
    var thumbnail: NSImage?
}
// Visual order is the index of the file within AppState.files — no separate field needed.

enum RenameRule: Identifiable {
    case prefix(String)
    case suffix(String)
    case findReplace(find: String, replace: String, caseSensitive: Bool)
    case regexFindReplace(pattern: String, replacement: String)
    case numberSequence(start: Int, step: Int, digits: Int, position: NumberPosition)
    case changeCase(CaseStyle)
    case insertAt(text: String, index: Int)
    case removeRange(from: Int, count: Int)
    case changeExtension(String)
    case dateBased(format: String, source: DateSource)
}

enum NumberPosition { case prefix, suffix, replaceAll }
enum CaseStyle { case lower, upper, title, camel }
enum DateSource { case fileModified, fileCreated, today }

struct RenameOperation {
    let timestamp: Date
    let changes: [(from: URL, to: URL)]
}
```

`AppState` (ObservableObject) holds:
- `files: [RenameFile]` — ordered array; index = visual order
- `rules: [RenameRule]` — ordered array; applied top-to-bottom
- `undoStack: [RenameOperation]` — capped at 10 entries
- `selectedFileID: UUID?` — drives the preview bar

---

## Architecture

Three layers, strictly separated:

### UI Layer (SwiftUI Views)
- `ContentView` — root layout, composes sidebar + canvas + rules panel + preview bar
- `OrderingCanvasView` — grid/list toggle, drag-reorder logic
- `FileCardView` / `FileRowView` — individual file representations
- `RulesPanelView` — stacked rule cards
- `RuleCardView` — per-rule configuration UI
- `ReviewSheetView` — before/after modal
- `SidebarView` — source info, filter, clear

Views are purely declarative. No disk access in views.

### State Layer
- `AppState: ObservableObject` — single source of truth, injected as `@EnvironmentObject`

### Services Layer
- `FileLoader` — handles all four loading methods, produces `[RenameFile]`
- `RenameEngine` — pure function: `compute(files:rules:) -> [String]`. No side effects.
- `ConflictDetector` — called by RenameEngine; flags duplicate output names
- `RenameExecutor` — calls `FileManager.moveItem` per file; records undo entry on success
- `ThumbnailService` — async thumbnail generation; uses `NSWorkspace` for system icons, `NSImage` for image files

---

## Rename Engine

`RenameEngine.compute(files: [RenameFile], rules: [RenameRule]) -> [String]`

- Takes the ordered file array and rule array
- For each file, applies enabled rules in sequence to produce the output filename
- The file's position in the `files` array is used by the numbering rule to produce sequence numbers matching visual order
- Returns one output name per file, in the same order as the input array
- Called reactively on every state change; result is written back to `file.computedName`

Rules are applied to the full filename stem (without extension) unless the rule explicitly targets the extension (Change Extension rule). Extension is re-appended after all stem rules run, unless the Change Extension rule replaces it. For files with no extension, the full filename is treated as the stem and no extension is appended.

---

## Undo

- `RenameExecutor` attempts each rename via `FileManager.moveItem`
- If all succeed: pushes a `RenameOperation` (list of from/to URL pairs) to `AppState.undoStack`
- If any fail: attempts to roll back completed renames before surfacing an error alert
- Undo: pops the top `RenameOperation`, calls `FileManager.moveItem` in reverse for each pair
- Undo stack is capped at 10 operations; oldest entry is dropped when the cap is exceeded
- Undo button in toolbar is disabled when the stack is empty

---

## Thumbnail Service

- Thumbnails are loaded asynchronously off the main thread
- For image files (jpg, png, gif, heic, tiff, webp): load via `NSImage(contentsOf:)`, downscale to card size
- For all other types: use `NSWorkspace.shared.icon(forFile:)` to get the system icon
- Thumbnails are cached in memory by file URL for the session lifetime
- Placeholder (grey rectangle) shown while thumbnail loads

---

## Error Handling

- **Load errors** (file unreadable, permission denied): file is skipped, a non-blocking banner is shown
- **Rename conflicts** (duplicate output names): blocked at the review sheet, shown in red
- **Rename failures** (disk error, permissions): alert shown with the specific file and error; partial renames are rolled back
- **Invalid regex**: inline error shown in the rule card, rule is disabled until fixed

---

## Testing Strategy

- **RenameEngine**: unit tested exhaustively — each rule type, rule combinations, edge cases (empty names, unicode, very long names, extension handling)
- **ConflictDetector**: unit tested for duplicate detection
- **RenameExecutor**: integration tested against a temp directory
- **AppState**: unit tested for undo stack cap, state transitions
- **UI**: manual testing for drag-reorder, grid/list toggle, review sheet flow

---

## Out of Scope (v1)

- Deep/recursive folder scanning
- EXIF/metadata-based naming (e.g. photo date from EXIF)
- Custom token/template system
- Cloud file support (iCloud Drive files are treated as local)
- Batch undo (undo multiple operations at once)
- Preset saving (save a rule configuration for reuse)
