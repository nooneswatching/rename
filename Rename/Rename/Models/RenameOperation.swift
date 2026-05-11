import Foundation

struct RenameChange {
    let from: URL
    let to: URL
}

struct RenameOperation {
    let timestamp: Date
    let changes: [RenameChange]

    init(changes: [RenameChange], timestamp: Date = Date()) {
        self.timestamp = timestamp
        self.changes = changes
    }
}
