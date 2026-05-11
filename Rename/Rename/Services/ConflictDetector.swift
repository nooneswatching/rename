import Foundation

enum ConflictDetector {
    /// Returns the set of names that appear more than once in the input array.
    static func findConflicts(in names: [String]) -> Set<String> {
        var seen = Set<String>()
        var conflicts = Set<String>()
        for name in names {
            if seen.contains(name) {
                conflicts.insert(name)
            } else {
                seen.insert(name)
            }
        }
        return conflicts
    }
}
