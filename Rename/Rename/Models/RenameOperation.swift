import Foundation

struct RenameOperation {
    let timestamp: Date
    let changes: [(from: URL, to: URL)]

    init(changes: [(from: URL, to: URL)], timestamp: Date = Date()) {
        self.timestamp = timestamp
        self.changes = changes
    }
}
