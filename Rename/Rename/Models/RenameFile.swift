import AppKit

struct RenameFile: Identifiable {
    let id: UUID
    var originalURL: URL
    var computedName: String
    var thumbnail: NSImage?

    init(originalURL: URL) {
        self.id = UUID()
        self.originalURL = originalURL
        self.computedName = originalURL.lastPathComponent
    }
}
