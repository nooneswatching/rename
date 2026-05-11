import Foundation

enum RenameExecutorError: LocalizedError {
    case renameFailed(from: URL, to: URL, underlying: Error)

    var errorDescription: String? {
        switch self {
        case .renameFailed(let from, let to, let err):
            return "Could not rename \"\(from.lastPathComponent)\" to \"\(to.lastPathComponent)\": \(err.localizedDescription)"
        }
    }
}

enum RenameExecutor {

    /// Renames all files in the change list atomically.
    /// On any failure, rolls back all completed renames before throwing.
    /// Returns a RenameOperation suitable for the undo stack on success.
    @discardableResult
    static func execute(changes: [RenameChange]) throws -> RenameOperation {
        var completed: [RenameChange] = []
        do {
            for change in changes {
                try FileManager.default.moveItem(at: change.from, to: change.to)
                completed.append(change)
            }
        } catch {
            // Rollback all completed renames in reverse order
            for done in completed.reversed() {
                try? FileManager.default.moveItem(at: done.to, to: done.from)
            }
            let failedIndex = completed.count
            guard failedIndex < changes.count else {
                throw error
            }
            let failed = changes[failedIndex]
            throw RenameExecutorError.renameFailed(from: failed.from, to: failed.to, underlying: error)
        }
        return RenameOperation(changes: changes)
    }

    /// Reverts a RenameOperation by moving files back to their original locations.
    static func undo(_ operation: RenameOperation) throws {
        for change in operation.changes.reversed() {
            try FileManager.default.moveItem(at: change.to, to: change.from)
        }
    }
}
