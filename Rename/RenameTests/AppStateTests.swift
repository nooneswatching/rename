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
        XCTAssertEqual(state.undoStack.count, 10)
        XCTAssertNotEqual(state.undoStack.first?.timestamp, first.timestamp)
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
