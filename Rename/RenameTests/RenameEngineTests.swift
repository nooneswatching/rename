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
    func test_insertAt_insertsAtStart() {
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

    func test_removeRange_zeroCount_isNoOp() {
        let result = compute([file("photo.jpg")], [item(.removeRange(from: 0, count: 0))])
        XCTAssertEqual(result, ["photo.jpg"])
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

    func test_numbering_stepGreaterThanOne() {
        let files = [file("x.jpg"), file("y.jpg"), file("z.jpg")]
        let result = compute(files, [item(.numberSequence(start: 0, step: 10, digits: 2, position: .prefix))])
        XCTAssertEqual(result, ["00_x.jpg", "10_y.jpg", "20_z.jpg"])
    }

    // MARK: Date
    func test_dateBased_today_prependsDateToStem() {
        let files = [file("photo.jpg")]
        let result = compute(files, [item(.dateBased(format: "yyyy", source: .today))])
        let year = String(Calendar.current.component(.year, from: Date()))
        XCTAssertEqual(result, ["\(year)_photo.jpg"])
    }
}
