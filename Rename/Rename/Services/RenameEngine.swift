import Foundation

enum RenameEngine {
    static func compute(files: [RenameFile], rules: [RenameRuleItem]) -> [String] {
        // Stub: returns original filenames until fully implemented in Task 4
        return files.map { $0.originalURL.lastPathComponent }
    }
}
