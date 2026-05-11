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
