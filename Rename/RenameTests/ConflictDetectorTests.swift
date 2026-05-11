import XCTest
@testable import Rename

final class ConflictDetectorTests: XCTestCase {

    func test_noConflicts_returnsEmptySet() {
        let names = ["a.jpg", "b.jpg", "c.jpg"]
        XCTAssertTrue(ConflictDetector.findConflicts(in: names).isEmpty)
    }

    func test_duplicateNames_returnedAsConflicts() {
        let names = ["a.jpg", "b.jpg", "a.jpg"]
        XCTAssertEqual(ConflictDetector.findConflicts(in: names), ["a.jpg"])
    }

    func test_multipleDuplicates_allReturned() {
        let names = ["a.jpg", "a.jpg", "b.jpg", "b.jpg", "c.jpg"]
        XCTAssertEqual(ConflictDetector.findConflicts(in: names), Set(["a.jpg", "b.jpg"]))
    }

    func test_emptyInput_returnsEmpty() {
        XCTAssertTrue(ConflictDetector.findConflicts(in: []).isEmpty)
    }

    func test_allSameName_returnsThatName() {
        let names = ["x.jpg", "x.jpg", "x.jpg"]
        XCTAssertEqual(ConflictDetector.findConflicts(in: names), ["x.jpg"])
    }

    func test_singleItem_noConflict() {
        XCTAssertTrue(ConflictDetector.findConflicts(in: ["only.jpg"]).isEmpty)
    }
}
