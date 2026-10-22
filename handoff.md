# Rename App — Handoff

## Where We Left Off

App is signed, notarized, and packaged as a drag-to-install DMG (`Rename.dmg` on Desktop). Sent to testers. Discussing what to build next.

---

## Recent Work (2026-05-12)

- UI cleanup: aligned panel headers to consistent 38pt bar, standardized empty state icon/text sizes
- Multi-select and remove: Finder-style click/cmd-click/shift-click, trash button, delete key, right-click context menu
- App icon implemented and icon cache cleared
- Signed with Developer ID Application: JOSHUA EARL NEWTON (J89TGDL779), notarized, stapled
- Packaged as drag-to-install DMG

---

## Windows Port (2026-10-22)

A native Windows version lives in `RenameWindows/` (WPF on .NET 10, Fluent theme). See `RenameWindows/README.md`.

- `Rename.Core` holds the engine/state with no UI dependency; `Rename.Core.Tests` ports the full Swift test suite (110 tests) and runs on any OS
- `Rename.App` is the WPF UI: same three-panel layout, grid/list canvas with drag-reorder and rubber-band selection, stacked rule cards, review dialog, undo
- CI: `.github/workflows/windows.yml` builds, tests, and uploads single-file `Rename.exe` artifacts for x64 and Arm64
- Not yet done: a hands-on UI pass on a real Windows machine (the port was built and unit tested on Linux), Authenticode signing, MSIX/Store packaging

---

## Project State

**Repo:** https://github.com/nooneswatching/rename
**Local path:** `/Users/joshuanewton/Library/CloudStorage/Dropbox/Development/Rename`
**Xcode project:** `Rename/Rename.xcodeproj`
**Branch:** `main` (all work committed and pushed, DMG not in repo)
**Last commit:** `40d8fc6` feat: add app icon

---

## Known Issues / Short Term Fixes

- Invalid regex silently no-ops instead of showing an error on the rule card
- Source folder name no longer shown after header redesign — useful context, consider restoring as subtitle or tooltip
- Empty state when filter matches nothing has no explanatory message

---

## Feature Ideas (prioritize based on tester feedback)

**High value**
- Undo for file removal from list (currently removed files can't be restored without re-loading)
- Persistent rules — rules reset on every launch; saving a default ruleset or named presets would be a big workflow improvement
- Folder output — option to copy renamed files to a new folder instead of renaming in place (non-destructive mode)

**Polish**
- Regex error feedback — inline error on rule card when regex is invalid
- Preview count — show how many files will be renamed before hitting Apply
- Conflict resolution hints — when there's a naming conflict, indicate which rule is causing it

**Power user**
- Rule presets — save/load named rule sets (e.g. "Photo import workflow")
- Auto-update via Sparkle framework — notifies users of new versions, no manual DMG re-download

---

## Distribution

- DMG packaging process: archive with xcodebuild → export with Developer ID → notarize with notarytool → staple → hdiutil DMG with Applications symlink
- Team ID: J89TGDL779
- Apple ID: josh.newton@gmail.com
- Next distribution step: Mac App Store (needs App Store provisioning profile + review)
