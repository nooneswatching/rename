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

    func test_makeFiles_returnsOneFilePerURL() {
        let urls = [makeFile("a.jpg"), makeFile("b.jpg")]
        let files = FileLoader.makeFiles(from: urls, existing: [])
        XCTAssertEqual(files.count, 2)
    }

    func test_makeFiles_deduplicatesAgainstExisting() {
        let url = makeFile("a.jpg")
        let existing = [RenameFile(originalURL: url)]
        let files = FileLoader.makeFiles(from: [url], existing: existing)
        XCTAssertTrue(files.isEmpty)
    }

    func test_makeFiles_deduplicatesWithinBatch() {
        let url = makeFile("a.jpg")
        let files = FileLoader.makeFiles(from: [url, url], existing: [])
        XCTAssertEqual(files.count, 1)
    }

    func test_makeFiles_skipsDirectories() {
        let dirURL = tempDir.appendingPathComponent("subdir")
        try! FileManager.default.createDirectory(at: dirURL, withIntermediateDirectories: true)
        let files = FileLoader.makeFiles(from: [dirURL], existing: [])
        XCTAssertTrue(files.isEmpty)
    }

    func test_topLevelFileURLs_returnsTopLevelFilesOnly() throws {
        _ = makeFile("a.txt")
        _ = makeFile("b.txt")
        let subdir = tempDir.appendingPathComponent("sub")
        try FileManager.default.createDirectory(at: subdir, withIntermediateDirectories: true)
        FileManager.default.createFile(atPath: subdir.appendingPathComponent("c.txt").path, contents: Data())

        let urls = FileLoader.topLevelFileURLs(in: tempDir)
        XCTAssertEqual(urls.count, 2)
        XCTAssertFalse(urls.contains(subdir.appendingPathComponent("c.txt")))
    }

    func test_topLevelFileURLs_excludesHiddenFiles() {
        _ = makeFile("visible.txt")
        _ = makeFile(".hidden")
        let urls = FileLoader.topLevelFileURLs(in: tempDir)
        XCTAssertEqual(urls.count, 1)
        XCTAssertEqual(urls.first?.lastPathComponent, "visible.txt")
    }

    func test_topLevelFileURLs_sortedAlphabetically() {
        _ = makeFile("c.txt")
        _ = makeFile("a.txt")
        _ = makeFile("b.txt")
        let urls = FileLoader.topLevelFileURLs(in: tempDir)
        XCTAssertEqual(urls.map(\.lastPathComponent), ["a.txt", "b.txt", "c.txt"])
    }
}
