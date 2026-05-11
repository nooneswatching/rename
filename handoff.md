# Rename App — Handoff

## Where We Left Off

Discussing how to package and distribute the app to peers. The user wants a **downloadable app with an installer file** (a signed, notarized `.pkg`).

### Next Step
Determine if the user has an **Apple Developer account** ($99/year). The answer shapes the path:

- **Has account** → sign the app with Developer ID certificate, notarize with `notarytool`, package as `.pkg` with `pkgbuild`/`productbuild`
- **No account** → unsigned zip is the only option (peers must right-click → Open to bypass Gatekeeper)

Resume by asking: "Do you have an Apple Developer account set up?"

---

## Project State

**Repo:** https://github.com/nooneswatching/rename  
**Local path:** `/Users/joshuanewton/Library/CloudStorage/Dropbox/Development/Rename`  
**Xcode project:** `Rename/Rename.xcodeproj`  
**Branch:** `main` (all work committed and pushed)  
**Tests:** 64 passing

### Recent commits (newest first)
- `feat: add Custom Name template rule with {name} and {n} tokens`
- `fix: replace List/onMove with ScrollView/onDrag/onDrop for list reorder`
- `fix: update onChange to two-parameter form, removing deprecation warnings`
- `fix: restore onDrag/onDrop for grid reorder, load UUID from provider in performDrop`
- `fix: stable UUIDs after rename/undo, disable list reorder with filter, wire rule reorder`

### Known issues / future work
- Drag-and-drop in grid mode works via `onDrag`/`onDrop` — test further if users report issues
- Invalid regex silently no-ops instead of disabling the rule (spec gap, low priority)
- `LabeledTextField` in RuleCardView uses local `@State` — external changes won't reflect after initial render (acceptable for current use)
- Distribution / packaging not yet done

---

## App Summary

Native macOS batch file renaming app. Key features:
- Visual ordering canvas (grid + list) — drag files to set numbering order
- Rules panel with 11 rule types including the new **Custom Name** template rule
- Custom Name rule: type a format like `Claude_{n}` with `{name}` and `{n}` tokens, configurable digit width and start number
- Live before/after preview, conflict detection, review sheet before applying
- Rename with undo (10-level stack)
- Four file loading methods: Open Folder, Add Files, drag files, drag folder
