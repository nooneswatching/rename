import XCTest
@testable import Rename

@MainActor
final class AppStateTests: XCTestCase {

    func test_initialState_isEmpty() {
        let state = AppState()
        XCTAssertTrue(state.files.isEmpty)
        XCTAssertTrue(state.rules.isEmpty)
        XCTAssertTrue(state.undoStack.isEmpty)
        XCTAssertTrue(state.selectedFileIDs.isEmpty)
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

    // MARK: - removeFiles

    func test_removeFiles_removesOnlyTargetedFiles() {
        let state = AppState()
        let a = RenameFile(originalURL: URL(fileURLWithPath: "/a.txt"))
        let b = RenameFile(originalURL: URL(fileURLWithPath: "/b.txt"))
        let c = RenameFile(originalURL: URL(fileURLWithPath: "/c.txt"))
        state.files = [a, b, c]
        state.removeFiles(ids: [a.id, c.id])
        XCTAssertEqual(state.files.map(\.id), [b.id])
    }

    func test_removeFiles_removingAllFilesLeavesEmptyArray() {
        let state = AppState()
        let a = RenameFile(originalURL: URL(fileURLWithPath: "/a.txt"))
        let b = RenameFile(originalURL: URL(fileURLWithPath: "/b.txt"))
        state.files = [a, b]
        state.removeFiles(ids: [a.id, b.id])
        XCTAssertTrue(state.files.isEmpty)
    }

    func test_removeFiles_prunesSelectedFileIDs() {
        let state = AppState()
        let a = RenameFile(originalURL: URL(fileURLWithPath: "/a.txt"))
        let b = RenameFile(originalURL: URL(fileURLWithPath: "/b.txt"))
        state.files = [a, b]
        state.selectedFileIDs = [a.id, b.id]
        state.removeFiles(ids: [a.id])
        XCTAssertEqual(state.selectedFileIDs, [b.id])
    }

    func test_removeFiles_unknownIDsAreNoOp() {
        let state = AppState()
        let a = RenameFile(originalURL: URL(fileURLWithPath: "/a.txt"))
        state.files = [a]
        state.removeFiles(ids: [UUID()])
        XCTAssertEqual(state.files.count, 1)
    }
}
