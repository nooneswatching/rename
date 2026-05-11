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

    func test_execute_renamesFile() throws {
        let from = makeFile("old.txt")
        let to = tempDir.appendingPathComponent("new.txt")
        let op = try RenameExecutor.execute(changes: [RenameChange(from: from, to: to)])
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
            RenameChange(from: from1, to: to1),
            RenameChange(from: from2, to: to2)
        ]))

        // a.txt should be rolled back to its original location
        XCTAssertTrue(FileManager.default.fileExists(atPath: from1.path))
        XCTAssertFalse(FileManager.default.fileExists(atPath: to1.path))
    }

    func test_undo_revertsRename() throws {
        let from = makeFile("original.txt")
        let to = tempDir.appendingPathComponent("renamed.txt")
        let op = try RenameExecutor.execute(changes: [RenameChange(from: from, to: to)])
        try RenameExecutor.undo(op)
        XCTAssertTrue(FileManager.default.fileExists(atPath: from.path))
        XCTAssertFalse(FileManager.default.fileExists(atPath: to.path))
    }

    func test_execute_multipleFiles() throws {
        let from1 = makeFile("1.txt")
        let from2 = makeFile("2.txt")
        let to1 = tempDir.appendingPathComponent("one.txt")
        let to2 = tempDir.appendingPathComponent("two.txt")
        _ = try RenameExecutor.execute(changes: [
            RenameChange(from: from1, to: to1),
            RenameChange(from: from2, to: to2)
        ])
        XCTAssertTrue(FileManager.default.fileExists(atPath: to1.path))
        XCTAssertTrue(FileManager.default.fileExists(atPath: to2.path))
    }
}
