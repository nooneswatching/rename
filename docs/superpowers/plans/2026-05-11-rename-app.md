# Rename App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a native macOS SwiftUI batch file renaming app with a visual drag-to-order canvas that drives rename numbering.

**Architecture:** Three-layer app (Views → AppState → Services). `AppState` is the single `ObservableObject` source of truth. `RenameEngine` is a pure function — no side effects — called reactively on every state change. Views never touch the disk directly.

**Tech Stack:** Swift 5.9+, SwiftUI, AppKit (NSOpenPanel, NSWorkspace, NSImage), XCTest, macOS 13+

---

## File Map

```
Rename/                              # Xcode app target
├── RenameApp.swift                  # @main entry, window setup
├── Models/
│   ├── RenameFile.swift             # RenameFile struct
│   ├── RenameRule.swift             # RenameRule enum, RenameRuleItem wrapper, supporting enums
│   └── RenameOperation.swift        # RenameOperation struct (undo entry)
├── State/
│   └── AppState.swift               # ObservableObject, recompute trigger, undo stack
├── Services/
│   ├── FileLoader.swift             # NSOpenPanel + drag-drop URL resolution
│   ├── RenameEngine.swift           # Pure compute(files:rules:) -> [String]
│   ├── ConflictDetector.swift       # Flags duplicate output names
│   ├── RenameExecutor.swift         # FileManager.moveItem + rollback
│   └── ThumbnailService.swift       # Async thumbnails + system icon fallback
└── Views/
    ├── ContentView.swift            # Root layout + window-level drop target
    ├── SidebarView.swift            # Source info, extension filter, Clear All
    ├── PreviewBarView.swift         # Before→After for selected file + Apply button
    ├── ReviewSheetView.swift        # Before/After modal with conflict highlighting
    ├── Canvas/
    │   ├── OrderingCanvasView.swift # Grid/list toggle, houses card/row views
    │   ├── FileCardView.swift       # Grid card (thumbnail + names)
    │   └── FileRowView.swift        # List row (icon + names + drag handle)
    └── Rules/
        ├── RulesPanelView.swift     # Stacked reorderable rule cards
        └── RuleCardView.swift       # Per-rule UI (toggle + config controls)

RenameTests/                         # XCTest target
├── RenameEngineTests.swift
├── ConflictDetectorTests.swift
├── FileLoaderTests.swift
├── RenameExecutorTests.swift
└── AppStateTests.swift
```

---

## Task 1: Create Xcode Project

**Files:**
- Create: Xcode project at `/Users/joshuanewton/Library/CloudStorage/Dropbox/Development/Rename/Rename.xcodeproj`

- [ ] **Step 1: Create the Xcode project**

Open Xcode → File → New → Project → macOS → App.

Settings:
- Product Name: `Rename`
- Team: (your team or none)
- Organization Identifier: `com.yourname` (any reverse-domain)
- Interface: **SwiftUI**
- Language: **Swift**
- Include Tests: **yes**

Save to: `/Users/joshuanewton/Library/CloudStorage/Dropbox/Development/Rename/`

Xcode will create `Rename.xcodeproj` and a `Rename/` folder with `RenameApp.swift`, `ContentView.swift`, and `Assets.xcassets`.

- [ ] **Step 2: Set minimum deployment target**

In the project navigator, click the `Rename` project → `Rename` target → General → Minimum Deployments → macOS **13.0**.

- [ ] **Step 3: Disable App Sandbox for local development**

In `Rename/Rename.entitlements` (Xcode creates this automatically), set:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.app-sandbox</key>
    <false/>
</dict>
</plist>
```

This allows the app to read/write any file on disk. Re-enable with proper entitlements before any App Store submission.

- [ ] **Step 4: Create folder groups in Xcode**

In Xcode's project navigator, create groups (New Group, not New Group with Folder — then set the file system path):
- `Models/`
- `State/`
- `Services/`
- `Views/Canvas/`
- `Views/Rules/`

- [ ] **Step 5: Verify the project builds**

In Xcode: Product → Build (⌘B). Expected: Build Succeeded.

- [ ] **Step 6: Initial commit**

```bash
cd /Users/joshuanewton/Library/CloudStorage/Dropbox/Development/Rename
git init
git add .
git commit -m "chore: scaffold Xcode project"
```

---

## Task 2: Data Models

**Files:**
- Create: `Rename/Models/RenameFile.swift`
- Create: `Rename/Models/RenameRule.swift`
- Create: `Rename/Models/RenameOperation.swift`
- Test: `RenameTests/ModelsTests.swift`

- [ ] **Step 1: Write the failing test**

Create `RenameTests/ModelsTests.swift`:

```swift
import XCTest
@testable import Rename

final class ModelsTests: XCTestCase {

    func test_renameFile_hasStableID() {
        let url = URL(fileURLWithPath: "/tmp/test.jpg")
        let file = RenameFile(originalURL: url)
        let id = file.id
        XCTAssertEqual(file.id, id)
    }

    func test_renameFile_computedNameDefaultsToOriginal() {
        let url = URL(fileURLWithPath: "/tmp/photo.jpg")
        let file = RenameFile(originalURL: url)
        XCTAssertEqual(file.computedName, "photo.jpg")
    }

    func test_renameRuleItem_defaultsToEnabled() {
        let item = RenameRuleItem(rule: .prefix("IMG_"))
        XCTAssertTrue(item.isEnabled)
    }

    func test_renameOperation_storesChanges() {
        let from = URL(fileURLWithPath: "/tmp/old.jpg")
        let to = URL(fileURLWithPath: "/tmp/new.jpg")
        let op = RenameOperation(changes: [(from: from, to: to)])
        XCTAssertEqual(op.changes.count, 1)
        XCTAssertEqual(op.changes[0].from, from)
        XCTAssertEqual(op.changes[0].to, to)
    }
}
```

- [ ] **Step 2: Run to verify failure**

In Xcode: Product → Test (⌘U), or:
```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(error:|FAILED|passed)"
```
Expected: compile errors (types not defined yet).

- [ ] **Step 3: Create `RenameFile.swift`**

```swift
import AppKit

struct RenameFile: Identifiable {
    let id: UUID
    let originalURL: URL
    var computedName: String
    var thumbnail: NSImage?

    init(originalURL: URL) {
        self.id = UUID()
        self.originalURL = originalURL
        self.computedName = originalURL.lastPathComponent
    }
}
```

- [ ] **Step 4: Create `RenameRule.swift`**

```swift
import Foundation

// Supporting enums
enum NumberPosition: String, CaseIterable {
    case prefix = "Prefix"
    case suffix = "Suffix"
    case replaceAll = "Replace All"
}

enum CaseStyle: String, CaseIterable {
    case lower = "lowercase"
    case upper = "UPPERCASE"
    case title = "Title Case"
    case camel = "camelCase"
}

enum DateSource: String, CaseIterable {
    case fileModified = "File Modified"
    case fileCreated = "File Created"
    case today = "Today"
}

// Core rule enum
enum RenameRule {
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

    var displayName: String {
        switch self {
        case .prefix: return "Prefix"
        case .suffix: return "Suffix"
        case .findReplace: return "Find & Replace"
        case .regexFindReplace: return "Regex Find & Replace"
        case .numberSequence: return "Numbering"
        case .changeCase: return "Change Case"
        case .insertAt: return "Insert at Position"
        case .removeRange: return "Remove Range"
        case .changeExtension: return "Change Extension"
        case .dateBased: return "Date-based Naming"
        }
    }
}

// Identifiable wrapper used by AppState and SwiftUI lists
struct RenameRuleItem: Identifiable {
    let id: UUID
    var rule: RenameRule
    var isEnabled: Bool

    init(rule: RenameRule, isEnabled: Bool = true) {
        self.id = UUID()
        self.rule = rule
        self.isEnabled = isEnabled
    }
}
```

- [ ] **Step 5: Create `RenameOperation.swift`**

```swift
import Foundation

struct RenameOperation {
    let timestamp: Date
    let changes: [(from: URL, to: URL)]

    init(changes: [(from: URL, to: URL)], timestamp: Date = Date()) {
        self.timestamp = timestamp
        self.changes = changes
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(Test Suite|error:|FAILED)"
```
Expected: `Test Suite 'All tests' passed`

- [ ] **Step 7: Commit**

```bash
git add Rename/Models/ RenameTests/ModelsTests.swift
git commit -m "feat: add data models (RenameFile, RenameRule, RenameOperation)"
```

---

## Task 3: AppState Skeleton

**Files:**
- Create: `Rename/State/AppState.swift`
- Test: `RenameTests/AppStateTests.swift`

- [ ] **Step 1: Write the failing test**

Create `RenameTests/AppStateTests.swift`:

```swift
import XCTest
@testable import Rename

@MainActor
final class AppStateTests: XCTestCase {

    func test_initialState_isEmpty() {
        let state = AppState()
        XCTAssertTrue(state.files.isEmpty)
        XCTAssertTrue(state.rules.isEmpty)
        XCTAssertTrue(state.undoStack.isEmpty)
        XCTAssertNil(state.selectedFileID)
    }

    func test_undoStack_cappedAtTen() {
        let state = AppState()
        for _ in 0..<15 {
            state.pushUndo(RenameOperation(changes: []))
        }
        XCTAssertEqual(state.undoStack.count, 10)
    }

    func test_undoStack_dropsOldestWhenCapped() {
        let state = AppState()
        let first = RenameOperation(changes: [], timestamp: Date(timeIntervalSince1970: 1))
        state.pushUndo(first)
        for i in 2...11 {
            state.pushUndo(RenameOperation(changes: [], timestamp: Date(timeIntervalSince1970: Double(i))))
        }
        // First entry should be gone, newest should be at top
        XCTAssertEqual(state.undoStack.count, 10)
        XCTAssertNotEqual(state.undoStack.last?.timestamp, first.timestamp)
    }

    func test_canUndo_falseWhenStackEmpty() {
        let state = AppState()
        XCTAssertFalse(state.canUndo)
    }

    func test_canUndo_trueWhenStackHasEntry() {
        let state = AppState()
        state.pushUndo(RenameOperation(changes: []))
        XCTAssertTrue(state.canUndo)
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(error:|FAILED)"
```
Expected: compile errors (AppState not defined).

- [ ] **Step 3: Create `AppState.swift`**

```swift
import Foundation
import Combine

@MainActor
final class AppState: ObservableObject {
    @Published var files: [RenameFile] = [] {
        didSet { recomputeNames() }
    }
    @Published var rules: [RenameRuleItem] = [] {
        didSet { recomputeNames() }
    }
    @Published var undoStack: [RenameOperation] = []
    @Published var selectedFileID: UUID? = nil

    private let maxUndoDepth = 10

    var canUndo: Bool { !undoStack.isEmpty }

    func pushUndo(_ operation: RenameOperation) {
        undoStack.append(operation)
        if undoStack.count > maxUndoDepth {
            undoStack.removeFirst()
        }
    }

    func popUndo() -> RenameOperation? {
        undoStack.isEmpty ? nil : undoStack.removeLast()
    }

    private func recomputeNames() {
        let names = RenameEngine.compute(files: files, rules: rules)
        for (index, name) in names.enumerated() {
            files[index].computedName = name
        }
    }
}
```

- [ ] **Step 4: Add a stub `RenameEngine` so the project compiles**

Create `Rename/Services/RenameEngine.swift` with a stub:

```swift
import Foundation

enum RenameEngine {
    static func compute(files: [RenameFile], rules: [RenameRuleItem]) -> [String] {
        // Stub: return original names until fully implemented
        return files.map { $0.originalURL.lastPathComponent }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(Test Suite|error:|FAILED)"
```
Expected: `Test Suite 'All tests' passed`

- [ ] **Step 6: Commit**

```bash
git add Rename/State/AppState.swift Rename/Services/RenameEngine.swift RenameTests/AppStateTests.swift
git commit -m "feat: add AppState with undo stack and recompute trigger"
```

---

## Task 4: RenameEngine — Stem Rules

Implement the rules that operate on the filename stem: prefix, suffix, findReplace, regexFindReplace, changeCase, insertAt, removeRange, changeExtension.

**Files:**
- Modify: `Rename/Services/RenameEngine.swift`
- Test: `RenameTests/RenameEngineTests.swift`

The engine splits each filename into stem + extension, applies stem rules, reattaches the extension (or replaces it if `changeExtension` is present). A file with no extension uses the full name as the stem.

- [ ] **Step 1: Write the failing tests**

Create `RenameTests/RenameEngineTests.swift`:

```swift
import XCTest
@testable import Rename

final class RenameEngineTests: XCTestCase {

    // MARK: Helpers
    private func file(_ name: String) -> RenameFile {
        RenameFile(originalURL: URL(fileURLWithPath: "/tmp/\(name)"))
    }

    private func item(_ rule: RenameRule, enabled: Bool = true) -> RenameRuleItem {
        RenameRuleItem(rule: rule, isEnabled: enabled)
    }

    private func compute(_ files: [RenameFile], _ rules: [RenameRuleItem]) -> [String] {
        RenameEngine.compute(files: files, rules: rules)
    }

    // MARK: Prefix
    func test_prefix_prependsToStem() {
        let result = compute([file("photo.jpg")], [item(.prefix("IMG_"))])
        XCTAssertEqual(result, ["IMG_photo.jpg"])
    }

    func test_prefix_emptyStringIsNoOp() {
        let result = compute([file("photo.jpg")], [item(.prefix(""))])
        XCTAssertEqual(result, ["photo.jpg"])
    }

    // MARK: Suffix
    func test_suffix_appendsToStemBeforeExtension() {
        let result = compute([file("photo.jpg")], [item(.suffix("_edited"))])
        XCTAssertEqual(result, ["photo_edited.jpg"])
    }

    func test_suffix_noExtension() {
        let result = compute([file("README")], [item(.suffix("_v2"))])
        XCTAssertEqual(result, ["README_v2"])
    }

    // MARK: Find & Replace
    func test_findReplace_replacesOccurrences() {
        let result = compute([file("my photo.jpg")], [item(.findReplace(find: " ", replace: "_", caseSensitive: true))])
        XCTAssertEqual(result, ["my_photo.jpg"])
    }

    func test_findReplace_caseInsensitive() {
        let result = compute([file("Photo.jpg")], [item(.findReplace(find: "photo", replace: "img", caseSensitive: false))])
        XCTAssertEqual(result, ["img.jpg"])
    }

    func test_findReplace_caseSensitiveNoMatch() {
        let result = compute([file("Photo.jpg")], [item(.findReplace(find: "photo", replace: "img", caseSensitive: true))])
        XCTAssertEqual(result, ["Photo.jpg"])
    }

    // MARK: Regex Find & Replace
    func test_regexFindReplace_basic() {
        let result = compute([file("IMG_001.jpg")], [item(.regexFindReplace(pattern: "\\d+", replacement: "999"))])
        XCTAssertEqual(result, ["IMG_999.jpg"])
    }

    func test_regexFindReplace_captureGroup() {
        let result = compute([file("2024_photo.jpg")], [item(.regexFindReplace(pattern: "(\\d{4})_", replacement: "[$1]-"))])
        XCTAssertEqual(result, ["[2024]-photo.jpg"])
    }

    func test_regexFindReplace_invalidPatternLeavesNameUnchanged() {
        let result = compute([file("photo.jpg")], [item(.regexFindReplace(pattern: "[invalid", replacement: "x"))])
        XCTAssertEqual(result, ["photo.jpg"])
    }

    // MARK: Change Case
    func test_changeCase_lower() {
        let result = compute([file("MyPHOTO.jpg")], [item(.changeCase(.lower))])
        XCTAssertEqual(result, ["myphoto.jpg"])
    }

    func test_changeCase_upper() {
        let result = compute([file("my_photo.jpg")], [item(.changeCase(.upper))])
        XCTAssertEqual(result, ["MY_PHOTO.jpg"])
    }

    func test_changeCase_title() {
        let result = compute([file("my photo.jpg")], [item(.changeCase(.title))])
        XCTAssertEqual(result, ["My Photo.jpg"])
    }

    func test_changeCase_camel() {
        let result = compute([file("my photo file.jpg")], [item(.changeCase(.camel))])
        XCTAssertEqual(result, ["myPhotoFile.jpg"])
    }

    // MARK: Insert at Position
    func test_insertAt_insertsAtIndex() {
        let result = compute([file("photo.jpg")], [item(.insertAt(text: "IMG_", index: 0))])
        XCTAssertEqual(result, ["IMG_photo.jpg"])
    }

    func test_insertAt_midString() {
        let result = compute([file("photo.jpg")], [item(.insertAt(text: "_edit", index: 5))])
        XCTAssertEqual(result, ["photo_edit.jpg"])
    }

    func test_insertAt_indexBeyondEndAppendsToStem() {
        let result = compute([file("abc.jpg")], [item(.insertAt(text: "_X", index: 100))])
        XCTAssertEqual(result, ["abc_X.jpg"])
    }

    // MARK: Remove Range
    func test_removeRange_removesCharacters() {
        let result = compute([file("IMG_photo.jpg")], [item(.removeRange(from: 0, count: 4))])
        XCTAssertEqual(result, ["photo.jpg"])
    }

    func test_removeRange_clampsToBounds() {
        let result = compute([file("abc.jpg")], [item(.removeRange(from: 1, count: 100))])
        XCTAssertEqual(result, ["a.jpg"])
    }

    // MARK: Change Extension
    func test_changeExtension_replacesExtension() {
        let result = compute([file("photo.jpg")], [item(.changeExtension("png"))])
        XCTAssertEqual(result, ["photo.png"])
    }

    func test_changeExtension_addsExtensionToNoExtFile() {
        let result = compute([file("README")], [item(.changeExtension("md"))])
        XCTAssertEqual(result, ["README.md"])
    }

    func test_changeExtension_emptyRemovesExtension() {
        let result = compute([file("photo.jpg")], [item(.changeExtension(""))])
        XCTAssertEqual(result, ["photo"])
    }

    // MARK: Disabled rules
    func test_disabledRule_isSkipped() {
        let result = compute([file("photo.jpg")], [item(.prefix("IMG_"), enabled: false)])
        XCTAssertEqual(result, ["photo.jpg"])
    }

    // MARK: Rule ordering
    func test_rulesApplyInOrder() {
        // prefix first, then uppercase — prefix text also gets uppercased
        let result = compute(
            [file("photo.jpg")],
            [item(.prefix("img_")), item(.changeCase(.upper))]
        )
        XCTAssertEqual(result, ["IMG_PHOTO.jpg"])
    }

    // MARK: Multiple files
    func test_multipleFiles_allProcessed() {
        let files = [file("a.jpg"), file("b.jpg"), file("c.jpg")]
        let result = compute(files, [item(.prefix("X_"))])
        XCTAssertEqual(result, ["X_a.jpg", "X_b.jpg", "X_c.jpg"])
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(FAILED|error:)" | head -20
```
Expected: test failures (stub returns original names).

- [ ] **Step 3: Implement `RenameEngine.swift` — stem decomposition and stem rules**

Replace the stub in `Rename/Services/RenameEngine.swift`:

```swift
import Foundation

enum RenameEngine {

    static func compute(files: [RenameFile], rules: [RenameRuleItem]) -> [String] {
        let enabledRules = rules.filter(\.isEnabled).map(\.rule)
        return files.enumerated().map { index, file in
            applyRules(enabledRules, to: file, index: index)
        }
    }

    // MARK: - Private

    private static func applyRules(_ rules: [RenameRule], to file: RenameFile, index: Int) -> String {
        let url = file.originalURL
        var stem = url.deletingPathExtension().lastPathComponent
        var ext = url.pathExtension   // empty string if no extension
        var extensionOverride: String? = nil

        for rule in rules {
            switch rule {
            case .prefix(let text):
                stem = text + stem

            case .suffix(let text):
                stem = stem + text

            case .findReplace(let find, let replace, let caseSensitive):
                guard !find.isEmpty else { break }
                let options: String.CompareOptions = caseSensitive ? [] : [.caseInsensitive]
                stem = stem.replacingOccurrences(of: find, with: replace, options: options)

            case .regexFindReplace(let pattern, let replacement):
                stem = applyRegex(pattern: pattern, replacement: replacement, to: stem)

            case .changeCase(let style):
                stem = applyCase(style, to: stem)

            case .insertAt(let text, let index):
                let clamped = min(index, stem.count)
                let insertIndex = stem.index(stem.startIndex, offsetBy: clamped)
                stem.insert(contentsOf: text, at: insertIndex)

            case .removeRange(let from, let count):
                let startClamped = min(from, stem.count)
                let endClamped = min(startClamped + count, stem.count)
                if startClamped < endClamped {
                    let start = stem.index(stem.startIndex, offsetBy: startClamped)
                    let end = stem.index(stem.startIndex, offsetBy: endClamped)
                    stem.removeSubrange(start..<end)
                }

            case .changeExtension(let newExt):
                extensionOverride = newExt

            case .numberSequence, .dateBased:
                // Handled in Task 5
                break
            }
        }

        let finalExt = extensionOverride ?? ext
        if finalExt.isEmpty {
            return stem
        } else {
            return "\(stem).\(finalExt)"
        }
    }

    private static func applyRegex(pattern: String, replacement: String, to string: String) -> String {
        guard let regex = try? NSRegularExpression(pattern: pattern) else { return string }
        let range = NSRange(string.startIndex..., in: string)
        return regex.stringByReplacingMatches(in: string, range: range, withTemplate: replacement)
    }

    private static func applyCase(_ style: CaseStyle, to string: String) -> String {
        switch style {
        case .lower:
            return string.lowercased()
        case .upper:
            return string.uppercased()
        case .title:
            return string.capitalized
        case .camel:
            let words = string.components(separatedBy: CharacterSet.alphanumerics.inverted)
                              .filter { !$0.isEmpty }
            guard !words.isEmpty else { return string }
            let first = words[0].lowercased()
            let rest = words.dropFirst().map { $0.capitalized }
            return ([first] + rest).joined()
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(Test Suite|FAILED|error:)"
```
Expected: `Test Suite 'All tests' passed`

- [ ] **Step 5: Commit**

```bash
git add Rename/Services/RenameEngine.swift RenameTests/RenameEngineTests.swift
git commit -m "feat: implement RenameEngine stem rules (prefix, suffix, find/replace, case, insert, remove, extension)"
```

---

## Task 5: RenameEngine — Numbering and Date Rules

**Files:**
- Modify: `Rename/Services/RenameEngine.swift`
- Modify: `RenameTests/RenameEngineTests.swift`

- [ ] **Step 1: Write the failing tests**

Add to `RenameTests/RenameEngineTests.swift`:

```swift
    // MARK: Numbering
    func test_numbering_prefixSequence() {
        let files = [file("a.jpg"), file("b.jpg"), file("c.jpg")]
        let result = compute(files, [item(.numberSequence(start: 1, step: 1, digits: 2, position: .prefix))])
        XCTAssertEqual(result, ["01_a.jpg", "02_b.jpg", "03_c.jpg"])
    }

    func test_numbering_suffixSequence() {
        let files = [file("a.jpg"), file("b.jpg")]
        let result = compute(files, [item(.numberSequence(start: 10, step: 5, digits: 3, position: .suffix))])
        XCTAssertEqual(result, ["a_010.jpg", "b_015.jpg"])
    }

    func test_numbering_replaceAll() {
        let files = [file("a.jpg"), file("b.jpg"), file("c.jpg")]
        let result = compute(files, [item(.numberSequence(start: 1, step: 1, digits: 1, position: .replaceAll))])
        XCTAssertEqual(result, ["1.jpg", "2.jpg", "3.jpg"])
    }

    func test_numbering_zeroPadded() {
        let files = (1...12).map { file("\($0).jpg") }
        let result = compute(files, [item(.numberSequence(start: 1, step: 1, digits: 3, position: .replaceAll))])
        XCTAssertEqual(result.first, "001.jpg")
        XCTAssertEqual(result.last, "012.jpg")
    }

    // MARK: Date
    func test_dateBased_today_format() {
        let files = [file("photo.jpg")]
        let result = compute(files, [item(.dateBased(format: "yyyy", source: .today))])
        let year = Calendar.current.component(.year, from: Date())
        XCTAssertEqual(result, ["\(year)_photo.jpg"])
    }
```

- [ ] **Step 2: Run to verify failures**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep "FAILED"
```
Expected: new numbering/date tests fail.

- [ ] **Step 3: Implement numbering and date rules in `RenameEngine.swift`**

In `applyRules`, update the `numberSequence` and `dateBased` cases — they need the file index and the file's URL for metadata:

```swift
            case .numberSequence(let start, let step, let digits, let position):
                let number = start + index * step
                let padded = String(format: "%0\(digits)d", number)
                switch position {
                case .prefix:
                    stem = "\(padded)_\(stem)"
                case .suffix:
                    stem = "\(stem)_\(padded)"
                case .replaceAll:
                    stem = padded
                }

            case .dateBased(let format, let source):
                let date = resolvedDate(for: url, source: source)
                let formatter = DateFormatter()
                formatter.dateFormat = format
                let dateStr = formatter.string(from: date)
                stem = "\(dateStr)_\(stem)"
```

Also add the helper method to `RenameEngine`:

```swift
    private static func resolvedDate(for url: URL, source: DateSource) -> Date {
        switch source {
        case .today:
            return Date()
        case .fileModified:
            let attrs = try? FileManager.default.attributesOfItem(atPath: url.path)
            return (attrs?[.modificationDate] as? Date) ?? Date()
        case .fileCreated:
            let attrs = try? FileManager.default.attributesOfItem(atPath: url.path)
            return (attrs?[.creationDate] as? Date) ?? Date()
        }
    }
```

- [ ] **Step 4: Run all tests**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(Test Suite|FAILED|error:)"
```
Expected: `Test Suite 'All tests' passed`

- [ ] **Step 5: Commit**

```bash
git add Rename/Services/RenameEngine.swift RenameTests/RenameEngineTests.swift
git commit -m "feat: add numbering and date-based rules to RenameEngine"
```

---

## Task 6: ConflictDetector

**Files:**
- Create: `Rename/Services/ConflictDetector.swift`
- Test: `RenameTests/ConflictDetectorTests.swift`

- [ ] **Step 1: Write the failing test**

Create `RenameTests/ConflictDetectorTests.swift`:

```swift
import XCTest
@testable import Rename

final class ConflictDetectorTests: XCTestCase {

    func test_noConflicts_returnsEmptySet() {
        let names = ["a.jpg", "b.jpg", "c.jpg"]
        let conflicts = ConflictDetector.findConflicts(in: names)
        XCTAssertTrue(conflicts.isEmpty)
    }

    func test_duplicateNames_returnedAsConflicts() {
        let names = ["a.jpg", "b.jpg", "a.jpg"]
        let conflicts = ConflictDetector.findConflicts(in: names)
        XCTAssertEqual(conflicts, ["a.jpg"])
    }

    func test_multipleDuplicates_allReturned() {
        let names = ["a.jpg", "a.jpg", "b.jpg", "b.jpg", "c.jpg"]
        let conflicts = ConflictDetector.findConflicts(in: names)
        XCTAssertEqual(conflicts, Set(["a.jpg", "b.jpg"]))
    }

    func test_emptyInput_returnsEmpty() {
        XCTAssertTrue(ConflictDetector.findConflicts(in: []).isEmpty)
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep "FAILED"
```

- [ ] **Step 3: Create `ConflictDetector.swift`**

```swift
import Foundation

enum ConflictDetector {
    /// Returns the set of names that appear more than once in the input array.
    static func findConflicts(in names: [String]) -> Set<String> {
        var seen = Set<String>()
        var conflicts = Set<String>()
        for name in names {
            if seen.contains(name) {
                conflicts.insert(name)
            } else {
                seen.insert(name)
            }
        }
        return conflicts
    }
}
```

- [ ] **Step 4: Run tests**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(Test Suite|FAILED)"
```
Expected: `Test Suite 'All tests' passed`

- [ ] **Step 5: Commit**

```bash
git add Rename/Services/ConflictDetector.swift RenameTests/ConflictDetectorTests.swift
git commit -m "feat: add ConflictDetector"
```

---

## Task 7: FileLoader

**Files:**
- Create: `Rename/Services/FileLoader.swift`
- Test: `RenameTests/FileLoaderTests.swift`

`FileLoader` has two responsibilities: (1) a static method that converts a list of URLs into `[RenameFile]` with deduplication, and (2) `NSOpenPanel` wrappers called from the toolbar. Panel calls are not unit-tested (they require UI).

- [ ] **Step 1: Write the failing test**

Create `RenameTests/FileLoaderTests.swift`:

```swift
import XCTest
@testable import Rename

final class FileLoaderTests: XCTestCase {

    private var tempDir: URL!

    override func setUp() {
        super.setUp()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString)
        try! FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDown() {
        try? FileManager.default.removeItem(at: tempDir)
        super.tearDown()
    }

    private func makeFile(_ name: String) -> URL {
        let url = tempDir.appendingPathComponent(name)
        FileManager.default.createFile(atPath: url.path, contents: Data())
        return url
    }

    func test_urlsToFiles_returnsOneFilePerURL() {
        let urls = [makeFile("a.jpg"), makeFile("b.jpg")]
        let files = FileLoader.makeFiles(from: urls, existing: [])
        XCTAssertEqual(files.count, 2)
    }

    func test_urlsToFiles_deduplicatesAgainstExisting() {
        let url = makeFile("a.jpg")
        let existing = [RenameFile(originalURL: url)]
        let files = FileLoader.makeFiles(from: [url], existing: existing)
        XCTAssertTrue(files.isEmpty)
    }

    func test_urlsToFiles_skipsDirectories() {
        let dirURL = tempDir.appendingPathComponent("subdir")
        try! FileManager.default.createDirectory(at: dirURL, withIntermediateDirectories: true)
        let files = FileLoader.makeFiles(from: [dirURL], existing: [])
        XCTAssertTrue(files.isEmpty)
    }

    func test_urlsFromFolder_returnsTopLevelFilesOnly() {
        _ = makeFile("a.txt")
        _ = makeFile("b.txt")
        let subdir = tempDir.appendingPathComponent("sub")
        try! FileManager.default.createDirectory(at: subdir, withIntermediateDirectories: true)
        FileManager.default.createFile(atPath: subdir.appendingPathComponent("c.txt").path, contents: Data())

        let urls = FileLoader.topLevelFileURLs(in: tempDir)
        XCTAssertEqual(urls.count, 2)
        XCTAssertFalse(urls.contains(subdir.appendingPathComponent("c.txt")))
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep "FAILED"
```

- [ ] **Step 3: Create `FileLoader.swift`**

```swift
import AppKit

enum FileLoader {

    /// Converts a list of file URLs into new RenameFile objects, deduplicating against existing files.
    /// Directories are silently skipped.
    static func makeFiles(from urls: [URL], existing: [RenameFile]) -> [RenameFile] {
        let existingURLs = Set(existing.map(\.originalURL))
        return urls.compactMap { url in
            var isDir: ObjCBool = false
            guard FileManager.default.fileExists(atPath: url.path, isDirectory: &isDir),
                  !isDir.boolValue,
                  !existingURLs.contains(url) else { return nil }
            return RenameFile(originalURL: url)
        }
    }

    /// Returns all top-level files (not subdirectories) inside a folder URL.
    static func topLevelFileURLs(in folder: URL) -> [URL] {
        let contents = (try? FileManager.default.contentsOfDirectory(
            at: folder,
            includingPropertiesForKeys: [.isRegularFileKey],
            options: [.skipsHiddenFiles]
        )) ?? []
        return contents.filter { url in
            (try? url.resourceValues(forKeys: [.isRegularFileKey]).isRegularFile) == true
        }.sorted { $0.lastPathComponent < $1.lastPathComponent }
    }

    /// Opens an NSOpenPanel to select a folder. Calls completion with top-level file URLs or nil if cancelled.
    @MainActor
    static func openFolderPanel(completion: @escaping ([URL]) -> Void) {
        let panel = NSOpenPanel()
        panel.canChooseFiles = false
        panel.canChooseDirectories = true
        panel.allowsMultipleSelection = false
        panel.message = "Choose a folder to load files from"
        panel.begin { response in
            guard response == .OK, let url = panel.url else { completion([]); return }
            completion(topLevelFileURLs(in: url))
        }
    }

    /// Opens an NSOpenPanel for multi-file selection. Calls completion with chosen URLs or empty if cancelled.
    @MainActor
    static func openFilePanel(completion: @escaping ([URL]) -> Void) {
        let panel = NSOpenPanel()
        panel.canChooseFiles = true
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = true
        panel.message = "Choose files to rename"
        panel.begin { response in
            guard response == .OK else { completion([]); return }
            completion(panel.urls)
        }
    }
}
```

- [ ] **Step 4: Run tests**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(Test Suite|FAILED)"
```
Expected: `Test Suite 'All tests' passed`

- [ ] **Step 5: Commit**

```bash
git add Rename/Services/FileLoader.swift RenameTests/FileLoaderTests.swift
git commit -m "feat: add FileLoader (URL→RenameFile conversion, dedup, folder scan, open panels)"
```

---

## Task 8: ThumbnailService

**Files:**
- Create: `Rename/Services/ThumbnailService.swift`

No unit tests — thumbnail generation depends on the file system and AppKit's icon machinery. Manual verification in Task 16 when the grid view is built.

- [ ] **Step 1: Create `ThumbnailService.swift`**

```swift
import AppKit

actor ThumbnailService {
    static let shared = ThumbnailService()

    private var cache: [URL: NSImage] = [:]
    private let imageExtensions: Set<String> = ["jpg", "jpeg", "png", "gif", "heic", "heif", "tiff", "tif", "webp", "bmp"]
    private let targetSize = CGSize(width: 120, height: 120)

    func thumbnail(for url: URL) async -> NSImage {
        if let cached = cache[url] { return cached }
        let image = await loadImage(for: url)
        cache[url] = image
        return image
    }

    func clearCache() {
        cache.removeAll()
    }

    // MARK: Private

    private func loadImage(for url: URL) async -> NSImage {
        let ext = url.pathExtension.lowercased()
        if imageExtensions.contains(ext) {
            return await Task.detached(priority: .userInitiated) {
                guard let src = NSImage(contentsOf: url) else {
                    return NSWorkspace.shared.icon(forFile: url.path)
                }
                return self.downscale(src)
            }.value
        } else {
            return await Task.detached(priority: .userInitiated) {
                NSWorkspace.shared.icon(forFile: url.path)
            }.value
        }
    }

    private func downscale(_ image: NSImage) -> NSImage {
        let original = image.size
        guard original.width > 0, original.height > 0 else { return image }
        let scale = min(targetSize.width / original.width, targetSize.height / original.height)
        let newSize = CGSize(width: original.width * scale, height: original.height * scale)
        let result = NSImage(size: newSize)
        result.lockFocus()
        image.draw(in: CGRect(origin: .zero, size: newSize))
        result.unlockFocus()
        return result
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Rename/Services/ThumbnailService.swift
git commit -m "feat: add ThumbnailService (async, cached, image preview + system icon fallback)"
```

---

## Task 9: RenameExecutor

**Files:**
- Create: `Rename/Services/RenameExecutor.swift`
- Test: `RenameTests/RenameExecutorTests.swift`

- [ ] **Step 1: Write the failing test**

Create `RenameTests/RenameExecutorTests.swift`:

```swift
import XCTest
@testable import Rename

final class RenameExecutorTests: XCTestCase {

    private var tempDir: URL!

    override func setUp() {
        super.setUp()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString)
        try! FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDown() {
        try? FileManager.default.removeItem(at: tempDir)
        super.tearDown()
    }

    private func makeFile(_ name: String, content: String = "x") -> URL {
        let url = tempDir.appendingPathComponent(name)
        try! content.write(to: url, atomically: true, encoding: .utf8)
        return url
    }

    func test_execute_renamesFiles() throws {
        let from = makeFile("old.txt")
        let to = tempDir.appendingPathComponent("new.txt")
        let op = try RenameExecutor.execute(changes: [(from: from, to: to)])
        XCTAssertTrue(FileManager.default.fileExists(atPath: to.path))
        XCTAssertFalse(FileManager.default.fileExists(atPath: from.path))
        XCTAssertEqual(op.changes.count, 1)
    }

    func test_execute_rollsBackOnPartialFailure() throws {
        let from1 = makeFile("a.txt")
        let from2 = makeFile("b.txt")
        let to1 = tempDir.appendingPathComponent("a_new.txt")
        let to2 = tempDir.appendingPathComponent("nonexistent_dir/b_new.txt") // will fail

        XCTAssertThrowsError(try RenameExecutor.execute(changes: [
            (from: from1, to: to1),
            (from: from2, to: to2)
        ]))

        // a.txt should be rolled back to original location
        XCTAssertTrue(FileManager.default.fileExists(atPath: from1.path))
        XCTAssertFalse(FileManager.default.fileExists(atPath: to1.path))
    }

    func test_undo_revertsRename() throws {
        let from = makeFile("original.txt")
        let to = tempDir.appendingPathComponent("renamed.txt")
        let op = try RenameExecutor.execute(changes: [(from: from, to: to)])
        try RenameExecutor.undo(op)
        XCTAssertTrue(FileManager.default.fileExists(atPath: from.path))
        XCTAssertFalse(FileManager.default.fileExists(atPath: to.path))
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep "FAILED"
```

- [ ] **Step 3: Create `RenameExecutor.swift`**

```swift
import Foundation

enum RenameExecutorError: LocalizedError {
    case renameFailed(from: URL, to: URL, underlying: Error)

    var errorDescription: String? {
        switch self {
        case .renameFailed(let from, let to, let err):
            return "Could not rename \"\(from.lastPathComponent)\" to \"\(to.lastPathComponent)\": \(err.localizedDescription)"
        }
    }
}

enum RenameExecutor {

    /// Renames all files in the given change list. On any failure, rolls back completed renames
    /// and rethrows the error. Returns a RenameOperation for the undo stack on success.
    @discardableResult
    static func execute(changes: [(from: URL, to: URL)]) throws -> RenameOperation {
        var completed: [(from: URL, to: URL)] = []
        do {
            for change in changes {
                try FileManager.default.moveItem(at: change.from, to: change.to)
                completed.append(change)
            }
        } catch {
            // Rollback
            for done in completed.reversed() {
                try? FileManager.default.moveItem(at: done.to, to: done.from)
            }
            let failed = changes[completed.count]
            throw RenameExecutorError.renameFailed(from: failed.from, to: failed.to, underlying: error)
        }
        return RenameOperation(changes: changes)
    }

    /// Reverts a previously applied RenameOperation by moving files back.
    static func undo(_ operation: RenameOperation) throws {
        for change in operation.changes.reversed() {
            try FileManager.default.moveItem(at: change.to, to: change.from)
        }
    }
}
```

- [ ] **Step 4: Run all tests**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(Test Suite|FAILED)"
```
Expected: `Test Suite 'All tests' passed`

- [ ] **Step 5: Commit**

```bash
git add Rename/Services/RenameExecutor.swift RenameTests/RenameExecutorTests.swift
git commit -m "feat: add RenameExecutor with atomic rollback and undo support"
```

---

## Task 10: Wire AppState to Services

Connect `AppState` to `FileLoader`, `RenameExecutor`, and `ThumbnailService` so the UI has real actions to call.

**Files:**
- Modify: `Rename/State/AppState.swift`

- [ ] **Step 1: Add action methods to `AppState.swift`**

Replace the contents of `AppState.swift` with:

```swift
import Foundation
import AppKit

@MainActor
final class AppState: ObservableObject {

    @Published var files: [RenameFile] = [] {
        didSet { recomputeNames() }
    }
    @Published var rules: [RenameRuleItem] = [] {
        didSet { recomputeNames() }
    }
    @Published var undoStack: [RenameOperation] = []
    @Published var selectedFileID: UUID? = nil
    @Published var errorMessage: String? = nil
    @Published var showReviewSheet = false

    private let maxUndoDepth = 10

    var canUndo: Bool { !undoStack.isEmpty }

    var conflictedNames: Set<String> {
        ConflictDetector.findConflicts(in: files.map(\.computedName))
    }

    var hasConflicts: Bool { !conflictedNames.isEmpty }

    // MARK: File Loading

    func addFiles(from urls: [URL]) {
        let newFiles = FileLoader.makeFiles(from: urls, existing: files)
        files.append(contentsOf: newFiles)
        loadThumbnails(for: newFiles)
    }

    func addFilesFromFolder(_ folder: URL) {
        let urls = FileLoader.topLevelFileURLs(in: folder)
        addFiles(from: urls)
    }

    func clearFiles() {
        files = []
        selectedFileID = nil
    }

    func openFolderPicker() {
        FileLoader.openFolderPanel { [weak self] urls in
            self?.addFiles(from: urls)
        }
    }

    func openFilePicker() {
        FileLoader.openFilePanel { [weak self] urls in
            self?.addFiles(from: urls)
        }
    }

    // MARK: Ordering

    func moveFiles(from source: IndexSet, to destination: Int) {
        files.move(fromOffsets: source, toOffset: destination)
    }

    // MARK: Rules

    func addRule(_ rule: RenameRule) {
        rules.append(RenameRuleItem(rule: rule))
    }

    func moveRules(from source: IndexSet, to destination: Int) {
        rules.move(fromOffsets: source, toOffset: destination)
    }

    func removeRule(id: UUID) {
        rules.removeAll { $0.id == id }
    }

    func toggleRule(id: UUID) {
        guard let index = rules.firstIndex(where: { $0.id == id }) else { return }
        rules[index].isEnabled.toggle()
    }

    // MARK: Rename

    func applyRename() {
        guard !hasConflicts else { return }
        let changes = files.map { file -> (from: URL, to: URL) in
            let newURL = file.originalURL.deletingLastPathComponent()
                .appendingPathComponent(file.computedName)
            return (from: file.originalURL, to: newURL)
        }
        do {
            let operation = try RenameExecutor.execute(changes: changes)
            pushUndo(operation)
            // Update originalURLs to reflect the rename, preserving cached thumbnails
            for (index, change) in changes.enumerated() {
                let existingThumbnail = files[index].thumbnail
                files[index] = RenameFile(originalURL: change.to)
                files[index].thumbnail = existingThumbnail
            }
            // Reload thumbnails with new URLs
            let updated = files
            loadThumbnails(for: updated)
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func performUndo() {
        guard let operation = popUndo() else { return }
        do {
            try RenameExecutor.undo(operation)
            // Reverse the URL updates in files array
            let reversedURLs = Dictionary(uniqueKeysWithValues: operation.changes.map { ($0.to, $0.from) })
            files = files.map { file in
                if let original = reversedURLs[file.originalURL] {
                    return RenameFile(originalURL: original)
                }
                return file
            }
            loadThumbnails(for: files)
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    // MARK: Undo Stack

    func pushUndo(_ operation: RenameOperation) {
        undoStack.append(operation)
        if undoStack.count > maxUndoDepth {
            undoStack.removeFirst()
        }
    }

    func popUndo() -> RenameOperation? {
        undoStack.isEmpty ? nil : undoStack.removeLast()
    }

    // MARK: Private

    private func recomputeNames() {
        let names = RenameEngine.compute(files: files, rules: rules)
        for (index, name) in names.enumerated() {
            files[index].computedName = name
        }
    }

    private func loadThumbnails(for targets: [RenameFile]) {
        for file in targets {
            guard let index = files.firstIndex(where: { $0.id == file.id }) else { continue }
            Task {
                let image = await ThumbnailService.shared.thumbnail(for: file.originalURL)
                self.files[index].thumbnail = image
            }
        }
    }
}
```

- [ ] **Step 2: Run all tests to verify nothing regressed**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(Test Suite|FAILED)"
```
Expected: `Test Suite 'All tests' passed`

- [ ] **Step 3: Commit**

```bash
git add Rename/State/AppState.swift
git commit -m "feat: wire AppState to FileLoader, RenameExecutor, ThumbnailService"
```

---

## Task 11: App Entry Point and Window Setup

**Files:**
- Modify: `Rename/RenameApp.swift`

- [ ] **Step 1: Update `RenameApp.swift`**

```swift
import SwiftUI

@main
struct RenameApp: App {
    @StateObject private var appState = AppState()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(appState)
                .frame(minWidth: 900, minHeight: 600)
        }
        .windowStyle(.titleBar)
        .windowToolbarStyle(.unified)
        .commands {
            CommandGroup(replacing: .undoRedo) {
                Button("Undo Rename") {
                    appState.performUndo()
                }
                .keyboardShortcut("z", modifiers: .command)
                .disabled(!appState.canUndo)
            }
        }
    }
}
```

- [ ] **Step 2: Build to verify no errors**

```bash
xcodebuild build -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(error:|Build succeeded)"
```
Expected: `Build succeeded`

- [ ] **Step 3: Commit**

```bash
git add Rename/RenameApp.swift
git commit -m "feat: configure app entry point with window sizing and undo menu command"
```

---

## Task 12: ContentView (Root Layout)

**Files:**
- Modify: `Rename/Views/ContentView.swift`

- [ ] **Step 1: Replace `ContentView.swift`**

```swift
import SwiftUI

struct ContentView: View {
    @EnvironmentObject var appState: AppState
    @State private var showReviewSheet = false

    var body: some View {
        VStack(spacing: 0) {
            HSplitView {
                SidebarView()
                    .frame(minWidth: 160, maxWidth: 220)
                OrderingCanvasView()
                    .frame(minWidth: 400)
                RulesPanelView()
                    .frame(minWidth: 240, maxWidth: 320)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)

            PreviewBarView(onApply: { showReviewSheet = true })
                .frame(height: 48)
                .background(Color(NSColor.windowBackgroundColor))
                .overlay(Divider(), alignment: .top)
        }
        .sheet(isPresented: $showReviewSheet) {
            ReviewSheetView(isPresented: $showReviewSheet)
        }
        .alert("Error", isPresented: Binding(
            get: { appState.errorMessage != nil },
            set: { if !$0 { appState.errorMessage = nil } }
        )) {
            Button("OK") { appState.errorMessage = nil }
        } message: {
            Text(appState.errorMessage ?? "")
        }
        .onDrop(of: [.fileURL], isTargeted: nil) { providers in
            handleDrop(providers: providers)
            return true
        }
        .toolbar {
            ToolbarItemGroup(placement: .navigation) {
                Button(action: { appState.openFolderPicker() }) {
                    Label("Open Folder", systemImage: "folder")
                }
                Button(action: { appState.openFilePicker() }) {
                    Label("Add Files", systemImage: "plus")
                }
            }
            ToolbarItemGroup(placement: .primaryAction) {
                Button(action: { appState.performUndo() }) {
                    Label("Undo", systemImage: "arrow.uturn.backward")
                }
                .disabled(!appState.canUndo)
            }
        }
    }

    private func handleDrop(providers: [NSItemProvider]) {
        for provider in providers {
            provider.loadItem(forTypeIdentifier: "public.file-url", options: nil) { item, _ in
                guard let data = item as? Data,
                      let url = URL(dataRepresentation: data, relativeTo: nil) else { return }
                DispatchQueue.main.async {
                    var isDir: ObjCBool = false
                    if FileManager.default.fileExists(atPath: url.path, isDirectory: &isDir), isDir.boolValue {
                        appState.addFilesFromFolder(url)
                    } else {
                        appState.addFiles(from: [url])
                    }
                }
            }
        }
    }
}
```

- [ ] **Step 2: Add stub views so the project compiles**

Create placeholder stubs for views not yet built. Create `Rename/Views/SidebarView.swift`:

```swift
import SwiftUI
struct SidebarView: View {
    var body: some View { Text("Sidebar").frame(maxWidth: .infinity, maxHeight: .infinity) }
}
```

Create `Rename/Views/PreviewBarView.swift`:

```swift
import SwiftUI
struct PreviewBarView: View {
    var onApply: () -> Void
    var body: some View { Text("Preview Bar") }
}
```

Create `Rename/Views/ReviewSheetView.swift`:

```swift
import SwiftUI
struct ReviewSheetView: View {
    @Binding var isPresented: Bool
    var body: some View { Text("Review Sheet") }
}
```

Create `Rename/Views/Canvas/OrderingCanvasView.swift`:

```swift
import SwiftUI
struct OrderingCanvasView: View {
    var body: some View { Text("Canvas").frame(maxWidth: .infinity, maxHeight: .infinity) }
}
```

Create `Rename/Views/Rules/RulesPanelView.swift`:

```swift
import SwiftUI
struct RulesPanelView: View {
    var body: some View { Text("Rules").frame(maxWidth: .infinity, maxHeight: .infinity) }
}
```

- [ ] **Step 3: Build to verify no errors**

```bash
xcodebuild build -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(error:|Build succeeded)"
```
Expected: `Build succeeded`

- [ ] **Step 4: Commit**

```bash
git add Rename/Views/
git commit -m "feat: add ContentView root layout with toolbar, drop target, error alert, review sheet"
```

---

## Task 13: SidebarView

**Files:**
- Modify: `Rename/Views/SidebarView.swift`

- [ ] **Step 1: Replace `SidebarView.swift`**

```swift
import SwiftUI

struct SidebarView: View {
    @EnvironmentObject var appState: AppState
    @State private var extensionFilter: String = ""

    private var sourceSummary: String {
        let dirs = Set(appState.files.map { $0.originalURL.deletingLastPathComponent().path })
        switch dirs.count {
        case 0: return "No files loaded"
        case 1: return dirs.first.map { URL(fileURLWithPath: $0).lastPathComponent } ?? "—"
        default: return "Multiple sources"
        }
    }

    private var availableExtensions: [String] {
        let exts = Set(appState.files.map { $0.originalURL.pathExtension.lowercased() })
            .filter { !$0.isEmpty }
        return exts.sorted()
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            VStack(alignment: .leading, spacing: 4) {
                Text("Source")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Text(sourceSummary)
                    .font(.body)
                    .lineLimit(2)
                    .truncationMode(.middle)
                Text("\(appState.files.count) file\(appState.files.count == 1 ? "" : "s")")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            .padding(.horizontal)
            .padding(.top)

            Divider()

            if !availableExtensions.isEmpty {
                VStack(alignment: .leading, spacing: 6) {
                    Text("Filter by type")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .padding(.horizontal)

                    ScrollView {
                        VStack(alignment: .leading, spacing: 2) {
                            FilterRow(label: "All", isSelected: extensionFilter.isEmpty) {
                                extensionFilter = ""
                            }
                            ForEach(availableExtensions, id: \.self) { ext in
                                FilterRow(label: ".\(ext)", isSelected: extensionFilter == ext) {
                                    extensionFilter = ext
                                }
                            }
                        }
                        .padding(.horizontal, 8)
                    }
                }
            }

            Spacer()

            Button(role: .destructive, action: { appState.clearFiles() }) {
                Label("Clear All", systemImage: "trash")
                    .frame(maxWidth: .infinity)
            }
            .padding()
            .disabled(appState.files.isEmpty)
        }
        .onChange(of: extensionFilter) { _ in
            // Extension filter is observed by OrderingCanvasView via @EnvironmentObject
            // We expose it through AppState for simplicity
            appState.extensionFilter = extensionFilter
        }
    }
}

private struct FilterRow: View {
    let label: String
    let isSelected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Text(label)
                .font(.caption)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.vertical, 3)
                .padding(.horizontal, 6)
                .background(isSelected ? Color.accentColor.opacity(0.15) : Color.clear)
                .cornerRadius(4)
        }
        .buttonStyle(.plain)
    }
}
```

- [ ] **Step 2: Add `extensionFilter` to `AppState`**

Add the following to `AppState.swift` (inside the class body, after `errorMessage`):

```swift
    @Published var extensionFilter: String = ""
```

- [ ] **Step 3: Build**

```bash
xcodebuild build -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(error:|Build succeeded)"
```

- [ ] **Step 4: Commit**

```bash
git add Rename/Views/SidebarView.swift Rename/State/AppState.swift
git commit -m "feat: implement SidebarView (source summary, extension filter, clear all)"
```

---

## Task 14: FileCardView and FileRowView

**Files:**
- Create: `Rename/Views/Canvas/FileCardView.swift`
- Create: `Rename/Views/Canvas/FileRowView.swift`

- [ ] **Step 1: Create `FileCardView.swift`**

```swift
import SwiftUI

struct FileCardView: View {
    let file: RenameFile
    let isSelected: Bool
    let isConflict: Bool
    let onSelect: () -> Void

    var body: some View {
        VStack(spacing: 4) {
            ZStack {
                RoundedRectangle(cornerRadius: 8)
                    .fill(Color(NSColor.controlBackgroundColor))
                    .frame(width: 100, height: 80)

                if let thumbnail = file.thumbnail {
                    Image(nsImage: thumbnail)
                        .resizable()
                        .aspectRatio(contentMode: .fit)
                        .frame(width: 100, height: 80)
                        .clipShape(RoundedRectangle(cornerRadius: 8))
                } else {
                    ProgressView()
                        .frame(width: 100, height: 80)
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
                .frame(width: 100)
                .foregroundStyle(.secondary)

            Text(file.computedName)
                .font(.caption2)
                .lineLimit(2)
                .truncationMode(.middle)
                .frame(width: 100)
                .foregroundStyle(isConflict ? .red : .accentColor)
        }
        .padding(4)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(isSelected ? Color.accentColor.opacity(0.08) : Color.clear)
        )
        .onTapGesture { onSelect() }
    }
}
```

- [ ] **Step 2: Create `FileRowView.swift`**

```swift
import SwiftUI

struct FileRowView: View {
    let file: RenameFile
    let isSelected: Bool
    let isConflict: Bool
    let onSelect: () -> Void

    var body: some View {
        HStack(spacing: 10) {
            Image(systemName: "line.3.horizontal")
                .foregroundStyle(.tertiary)
                .frame(width: 20)

            if let thumbnail = file.thumbnail {
                Image(nsImage: thumbnail)
                    .resizable()
                    .aspectRatio(contentMode: .fit)
                    .frame(width: 20, height: 20)
            } else {
                Image(systemName: "doc")
                    .frame(width: 20, height: 20)
            }

            Text(file.originalURL.lastPathComponent)
                .lineLimit(1)
                .truncationMode(.middle)
                .foregroundStyle(.primary)
                .frame(maxWidth: .infinity, alignment: .leading)

            Image(systemName: "arrow.right")
                .foregroundStyle(.tertiary)
                .font(.caption)

            Text(file.computedName)
                .lineLimit(1)
                .truncationMode(.middle)
                .foregroundStyle(isConflict ? .red : .accentColor)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(.vertical, 4)
        .padding(.horizontal, 8)
        .background(isSelected ? Color.accentColor.opacity(0.1) : Color.clear)
        .cornerRadius(6)
        .contentShape(Rectangle())
        .onTapGesture { onSelect() }
    }
}
```

- [ ] **Step 3: Build**

```bash
xcodebuild build -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(error:|Build succeeded)"
```

- [ ] **Step 4: Commit**

```bash
git add Rename/Views/Canvas/FileCardView.swift Rename/Views/Canvas/FileRowView.swift
git commit -m "feat: add FileCardView (grid) and FileRowView (list)"
```

---

## Task 15: OrderingCanvasView

**Files:**
- Modify: `Rename/Views/Canvas/OrderingCanvasView.swift`

The grid uses `onDrag`/`onDrop` for reordering. The list uses SwiftUI's `List` with `onMove`.

- [ ] **Step 1: Replace `OrderingCanvasView.swift`**

```swift
import SwiftUI
import UniformTypeIdentifiers

enum CanvasMode: String, CaseIterable {
    case grid = "Grid"
    case list = "List"
}

struct OrderingCanvasView: View {
    @EnvironmentObject var appState: AppState
    @State private var mode: CanvasMode = .grid
    @State private var draggingID: UUID? = nil

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
                    .onDrag {
                        draggingID = file.id
                        return NSItemProvider(object: file.id.uuidString as NSString)
                    }
                    .onDrop(of: [UTType.plainText], delegate: GridDropDelegate(
                        targetFile: file,
                        appState: appState,
                        draggingID: $draggingID
                    ))
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
            .onMove { source, destination in
                appState.moveFiles(from: source, to: destination)
            }
        }
        .listStyle(.plain)
    }
}

// MARK: - Grid Drop Delegate

struct GridDropDelegate: DropDelegate {
    let targetFile: RenameFile
    let appState: AppState
    @Binding var draggingID: UUID?

    func performDrop(info: DropInfo) -> Bool {
        guard let draggingID,
              let fromIndex = appState.files.firstIndex(where: { $0.id == draggingID }),
              let toIndex = appState.files.firstIndex(where: { $0.id == targetFile.id }),
              fromIndex != toIndex else { return false }

        withAnimation {
            appState.files.move(
                fromOffsets: IndexSet(integer: fromIndex),
                toOffset: toIndex > fromIndex ? toIndex + 1 : toIndex
            )
        }
        self.draggingID = nil
        return true
    }

    func dropUpdated(info: DropInfo) -> DropProposal? {
        DropProposal(operation: .move)
    }

    func validateDrop(info: DropInfo) -> Bool { true }
}
```

- [ ] **Step 2: Build**

```bash
xcodebuild build -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(error:|Build succeeded)"
```

- [ ] **Step 3: Manually verify grid and list**

Run the app (⌘R). Load some files. Verify:
- Grid and list toggle works
- Files appear with thumbnails (or icons for non-images)
- Computed names appear below originals
- Drag reorder works in both modes

- [ ] **Step 4: Commit**

```bash
git add Rename/Views/Canvas/OrderingCanvasView.swift
git commit -m "feat: implement OrderingCanvasView with grid drag-reorder and list onMove"
```

---

## Task 16: RuleCardView

**Files:**
- Create: `Rename/Views/Rules/RuleCardView.swift`

Each rule type needs its own configuration controls inside a collapsible card.

- [ ] **Step 1: Create `RuleCardView.swift`**

```swift
import SwiftUI

struct RuleCardView: View {
    @Binding var item: RenameRuleItem
    let onDelete: () -> Void
    @State private var isExpanded = true

    var body: some View {
        VStack(spacing: 0) {
            // Header
            HStack {
                Toggle("", isOn: $item.isEnabled)
                    .labelsHidden()
                    .toggleStyle(.checkbox)

                Text(item.rule.displayName)
                    .font(.subheadline)
                    .fontWeight(.medium)

                Spacer()

                Button(action: { withAnimation { isExpanded.toggle() } }) {
                    Image(systemName: isExpanded ? "chevron.up" : "chevron.down")
                        .font(.caption)
                }
                .buttonStyle(.plain)

                Button(action: onDelete) {
                    Image(systemName: "xmark")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 8)

            if isExpanded {
                Divider()
                ruleControls
                    .padding(10)
            }
        }
        .background(Color(NSColor.controlBackgroundColor))
        .clipShape(RoundedRectangle(cornerRadius: 8))
        .overlay(
            RoundedRectangle(cornerRadius: 8)
                .stroke(Color(NSColor.separatorColor), lineWidth: 1)
        )
    }

    @ViewBuilder
    private var ruleControls: some View {
        switch item.rule {
        case .prefix(let text):
            LabeledTextField("Text", value: text) { item.rule = .prefix($0) }

        case .suffix(let text):
            LabeledTextField("Text", value: text) { item.rule = .suffix($0) }

        case .findReplace(let find, let replace, let cs):
            VStack(spacing: 6) {
                LabeledTextField("Find", value: find) { item.rule = .findReplace(find: $0, replace: replace, caseSensitive: cs) }
                LabeledTextField("Replace", value: replace) { item.rule = .findReplace(find: find, replace: $0, caseSensitive: cs) }
                Toggle("Case sensitive", isOn: Binding(get: { cs }, set: { item.rule = .findReplace(find: find, replace: replace, caseSensitive: $0) }))
                    .font(.caption)
            }

        case .regexFindReplace(let pattern, let replacement):
            VStack(spacing: 6) {
                LabeledTextField("Pattern", value: pattern) {
                    item.rule = .regexFindReplace(pattern: $0, replacement: replacement)
                }
                if !pattern.isEmpty, (try? NSRegularExpression(pattern: pattern)) == nil {
                    Text("Invalid regex pattern")
                        .font(.caption)
                        .foregroundStyle(.red)
                }
                LabeledTextField("Replacement", value: replacement) {
                    item.rule = .regexFindReplace(pattern: pattern, replacement: $0)
                }
            }

        case .numberSequence(let start, let step, let digits, let position):
            VStack(spacing: 6) {
                LabeledIntField("Start", value: start) { item.rule = .numberSequence(start: $0, step: step, digits: digits, position: position) }
                LabeledIntField("Step", value: step) { item.rule = .numberSequence(start: start, step: $0, digits: digits, position: position) }
                LabeledIntField("Digits (min)", value: digits) { item.rule = .numberSequence(start: start, step: step, digits: $0, position: position) }
                Picker("Position", selection: Binding(get: { position }, set: { item.rule = .numberSequence(start: start, step: step, digits: digits, position: $0) })) {
                    ForEach(NumberPosition.allCases, id: \.self) { Text($0.rawValue).tag($0) }
                }
                .font(.caption)
            }

        case .changeCase(let style):
            Picker("Case", selection: Binding(get: { style }, set: { item.rule = .changeCase($0) })) {
                ForEach(CaseStyle.allCases, id: \.self) { Text($0.rawValue).tag($0) }
            }

        case .insertAt(let text, let index):
            VStack(spacing: 6) {
                LabeledTextField("Text", value: text) { item.rule = .insertAt(text: $0, index: index) }
                LabeledIntField("At position", value: index) { item.rule = .insertAt(text: text, index: $0) }
            }

        case .removeRange(let from, let count):
            VStack(spacing: 6) {
                LabeledIntField("From position", value: from) { item.rule = .removeRange(from: $0, count: count) }
                LabeledIntField("Character count", value: count) { item.rule = .removeRange(from: from, count: $0) }
            }

        case .changeExtension(let ext):
            LabeledTextField("Extension", value: ext) { item.rule = .changeExtension($0) }

        case .dateBased(let format, let source):
            VStack(spacing: 6) {
                LabeledTextField("Format (e.g. yyyy-MM-dd)", value: format) { item.rule = .dateBased(format: $0, source: source) }
                Picker("Date source", selection: Binding(get: { source }, set: { item.rule = .dateBased(format: format, source: $0) })) {
                    ForEach(DateSource.allCases, id: \.self) { Text($0.rawValue).tag($0) }
                }
                .font(.caption)
            }
        }
    }
}

// MARK: - Helpers

private struct LabeledTextField: View {
    let label: String
    let value: String
    let onChange: (String) -> Void
    @State private var text: String

    init(_ label: String, value: String, onChange: @escaping (String) -> Void) {
        self.label = label
        self.value = value
        self.onChange = onChange
        self._text = State(initialValue: value)
    }

    var body: some View {
        HStack {
            Text(label)
                .font(.caption)
                .foregroundStyle(.secondary)
                .frame(width: 80, alignment: .leading)
            TextField("", text: $text)
                .textFieldStyle(.roundedBorder)
                .font(.caption)
                .onChange(of: text) { onChange($0) }
        }
    }
}

private struct LabeledIntField: View {
    let label: String
    let value: Int
    let onChange: (Int) -> Void
    @State private var text: String

    init(_ label: String, value: Int, onChange: @escaping (Int) -> Void) {
        self.label = label
        self.value = value
        self.onChange = onChange
        self._text = State(initialValue: "\(value)")
    }

    var body: some View {
        HStack {
            Text(label)
                .font(.caption)
                .foregroundStyle(.secondary)
                .frame(width: 80, alignment: .leading)
            TextField("", text: $text)
                .textFieldStyle(.roundedBorder)
                .font(.caption)
                .onChange(of: text) {
                    if let n = Int($0) { onChange(n) }
                }
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
xcodebuild build -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(error:|Build succeeded)"
```

- [ ] **Step 3: Commit**

```bash
git add Rename/Views/Rules/RuleCardView.swift
git commit -m "feat: implement RuleCardView with per-rule configuration controls"
```

---

## Task 17: RulesPanelView

**Files:**
- Modify: `Rename/Views/Rules/RulesPanelView.swift`

- [ ] **Step 1: Replace `RulesPanelView.swift`**

```swift
import SwiftUI

struct RulesPanelView: View {
    @EnvironmentObject var appState: AppState
    @State private var showAddMenu = false

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text("Rename Rules")
                    .font(.headline)
                Spacer()
                Button(action: { showAddMenu = true }) {
                    Image(systemName: "plus")
                }
                .buttonStyle(.plain)
                .popover(isPresented: $showAddMenu) {
                    AddRuleMenu(isPresented: $showAddMenu)
                }
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 10)

            Divider()

            if appState.rules.isEmpty {
                VStack(spacing: 8) {
                    Image(systemName: "wand.and.stars")
                        .font(.title2)
                        .foregroundStyle(.tertiary)
                    Text("Add a rule to start renaming")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView {
                    LazyVStack(spacing: 8) {
                        ForEach($appState.rules) { $item in
                            RuleCardView(item: $item) {
                                appState.removeRule(id: item.id)
                            }
                        }
                    }
                    .padding(10)
                    .animation(.default, value: appState.rules.map(\.id))
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(Color(NSColor.windowBackgroundColor))
    }
}

// MARK: - Add Rule Menu

private struct AddRuleMenu: View {
    @EnvironmentObject var appState: AppState
    @Binding var isPresented: Bool

    private let ruleTemplates: [(String, RenameRule)] = [
        ("Prefix", .prefix("")),
        ("Suffix", .suffix("")),
        ("Find & Replace", .findReplace(find: "", replace: "", caseSensitive: true)),
        ("Regex Find & Replace", .regexFindReplace(pattern: "", replacement: "")),
        ("Numbering", .numberSequence(start: 1, step: 1, digits: 2, position: .prefix)),
        ("Change Case", .changeCase(.lower)),
        ("Insert at Position", .insertAt(text: "", index: 0)),
        ("Remove Range", .removeRange(from: 0, count: 1)),
        ("Change Extension", .changeExtension("")),
        ("Date-based Naming", .dateBased(format: "yyyy-MM-dd", source: .fileModified)),
    ]

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text("Add Rule")
                .font(.caption)
                .foregroundStyle(.secondary)
                .padding(.horizontal, 12)
                .padding(.top, 8)

            ForEach(ruleTemplates, id: \.0) { name, rule in
                Button(action: {
                    appState.addRule(rule)
                    isPresented = false
                }) {
                    Text(name)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.horizontal, 12)
                        .padding(.vertical, 5)
                }
                .buttonStyle(.plain)
                .contentShape(Rectangle())
            }
        }
        .padding(.bottom, 8)
        .frame(width: 200)
    }
}
```

- [ ] **Step 2: Build**

```bash
xcodebuild build -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(error:|Build succeeded)"
```

- [ ] **Step 3: Manually verify rules panel**

Run the app. Add a few rules. Verify:
- Rules appear as collapsible cards
- Toggle enables/disables a rule
- Configuring a rule (e.g. typing a prefix) immediately updates computed names in the canvas

- [ ] **Step 4: Commit**

```bash
git add Rename/Views/Rules/RulesPanelView.swift
git commit -m "feat: implement RulesPanelView with add-rule menu and live recompute"
```

---

## Task 18: PreviewBarView

**Files:**
- Modify: `Rename/Views/PreviewBarView.swift`

- [ ] **Step 1: Replace `PreviewBarView.swift`**

```swift
import SwiftUI

struct PreviewBarView: View {
    @EnvironmentObject var appState: AppState
    let onApply: () -> Void

    private var previewFile: RenameFile? {
        if let id = appState.selectedFileID {
            return appState.files.first(where: { $0.id == id })
        }
        return appState.files.first
    }

    var body: some View {
        HStack(spacing: 16) {
            if let file = previewFile {
                Text(file.originalURL.lastPathComponent)
                    .lineLimit(1)
                    .truncationMode(.middle)
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .trailing)

                Image(systemName: "arrow.right")
                    .foregroundStyle(.tertiary)

                Text(file.computedName)
                    .lineLimit(1)
                    .truncationMode(.middle)
                    .foregroundStyle(appState.conflictedNames.contains(file.computedName) ? .red : .accentColor)
                    .frame(maxWidth: .infinity, alignment: .leading)
            } else {
                Text("No files loaded")
                    .foregroundStyle(.tertiary)
                    .frame(maxWidth: .infinity)
            }

            Button("Apply Rename") {
                onApply()
            }
            .buttonStyle(.borderedProminent)
            .disabled(appState.files.isEmpty || appState.hasConflicts)
            .help(appState.hasConflicts ? "Resolve naming conflicts before applying" : "")
        }
        .padding(.horizontal, 16)
    }
}
```

- [ ] **Step 2: Build**

```bash
xcodebuild build -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(error:|Build succeeded)"
```

- [ ] **Step 3: Commit**

```bash
git add Rename/Views/PreviewBarView.swift
git commit -m "feat: implement PreviewBarView with live before/after preview and Apply button"
```

---

## Task 19: ReviewSheetView

**Files:**
- Modify: `Rename/Views/ReviewSheetView.swift`

- [ ] **Step 1: Replace `ReviewSheetView.swift`**

```swift
import SwiftUI

struct ReviewSheetView: View {
    @EnvironmentObject var appState: AppState
    @Binding var isPresented: Bool

    private var conflicts: Set<String> { appState.conflictedNames }

    var body: some View {
        VStack(spacing: 0) {
            // Header
            HStack {
                Text("Review Renames")
                    .font(.headline)
                Spacer()
                if !conflicts.isEmpty {
                    Label("\(conflicts.count) conflict\(conflicts.count == 1 ? "" : "s")", systemImage: "exclamationmark.triangle.fill")
                        .font(.caption)
                        .foregroundStyle(.red)
                }
            }
            .padding()

            Divider()

            // Column headers
            HStack {
                Text("Original Name")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
                Text("New Name")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .padding(.horizontal)
            .padding(.vertical, 6)
            .background(Color(NSColor.controlBackgroundColor))

            Divider()

            // File list
            ScrollView {
                LazyVStack(spacing: 0) {
                    ForEach(Array(appState.files.enumerated()), id: \.element.id) { index, file in
                        HStack {
                            Text(file.originalURL.lastPathComponent)
                                .lineLimit(1)
                                .truncationMode(.middle)
                                .frame(maxWidth: .infinity, alignment: .leading)
                            Text(file.computedName)
                                .lineLimit(1)
                                .truncationMode(.middle)
                                .foregroundStyle(conflicts.contains(file.computedName) ? .red : .primary)
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }
                        .padding(.horizontal)
                        .padding(.vertical, 5)
                        .background(index % 2 == 0 ? Color.clear : Color(NSColor.controlBackgroundColor).opacity(0.4))

                        if index < appState.files.count - 1 {
                            Divider().padding(.horizontal)
                        }
                    }
                }
            }

            Divider()

            // Action buttons
            HStack {
                Button("Cancel") {
                    isPresented = false
                }
                .keyboardShortcut(.escape)

                Spacer()

                if !conflicts.isEmpty {
                    Text("Fix conflicts before confirming")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                Button("Confirm Rename") {
                    isPresented = false
                    appState.applyRename()
                }
                .buttonStyle(.borderedProminent)
                .keyboardShortcut(.return)
                .disabled(!conflicts.isEmpty)
            }
            .padding()
        }
        .frame(width: 600, height: 500)
    }
}
```

- [ ] **Step 2: Build**

```bash
xcodebuild build -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(error:|Build succeeded)"
```

- [ ] **Step 3: End-to-end manual test**

Run the app. Perform a full rename cycle:
1. Load a folder of test files (create a temp folder with a few files)
2. Reorder them in the canvas
3. Add a Numbering rule (prefix, start: 1, step: 1, digits: 2)
4. Verify computed names update live
5. Click Apply → Review sheet appears with correct before/after
6. Confirm → files are renamed on disk
7. Verify with Finder that files have the new names
8. Click Undo in toolbar → files are renamed back to originals
9. Verify with Finder

- [ ] **Step 4: Run full test suite**

```bash
xcodebuild test -scheme Rename -destination 'platform=macOS' 2>&1 | grep -E "(Test Suite|FAILED|error:)"
```
Expected: `Test Suite 'All tests' passed`

- [ ] **Step 5: Final commit**

```bash
git add Rename/Views/ReviewSheetView.swift
git commit -m "feat: implement ReviewSheetView with conflict highlighting and confirm action"
```

---

## Post-Implementation Notes

- **Sandbox:** App sandbox is disabled for local use. To distribute via App Store, re-enable sandboxing and add `com.apple.security.files.user-selected.read-write` to the entitlements.
- **`hoverEffect`:** Remove the `.hoverEffect(.highlight)` call in `RulesPanelView` if the compiler errors — this modifier is not available on macOS; use `.background(Color.accentColor.opacity(0.1))` on hover via `@State private var isHovering` + `.onHover` instead.
- **Extension filter scope:** The filter only hides files from the canvas view — it does not exclude them from rename operations. All files in `appState.files` are always renamed.
