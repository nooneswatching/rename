import Foundation

enum RenameEngine {

    static func compute(files: [RenameFile], rules: [RenameRuleItem]) -> [String] {
        let enabledRules = rules.filter(\.isEnabled).map(\.rule)
        return files.enumerated().map { index, file in
            applyRules(enabledRules, to: file, index: index)
        }
    }

    // MARK: - Private

    private static func applyRules(_ rules: [RenameRule], to file: RenameFile, index: Int) -> String {
        let url = file.originalURL
        var stem = url.deletingPathExtension().lastPathComponent
        let ext = url.pathExtension   // empty string if no extension
        var extensionOverride: String? = nil

        for rule in rules {
            switch rule {
            case .prefix(let text):
                stem = text + stem

            case .suffix(let text):
                stem = stem + text

            case .findReplace(let find, let replace, let caseSensitive):
                guard !find.isEmpty else { continue }
                let options: String.CompareOptions = caseSensitive ? [] : [.caseInsensitive]
                stem = stem.replacingOccurrences(of: find, with: replace, options: options)

            case .regexFindReplace(let pattern, let replacement):
                stem = applyRegex(pattern: pattern, replacement: replacement, to: stem)

            case .changeCase(let style):
                stem = applyCase(style, to: stem)

            case .insertAt(let text, let index):
                let clamped = max(0, min(index, stem.count))
                let insertIndex = stem.index(stem.startIndex, offsetBy: clamped)
                stem.insert(contentsOf: text, at: insertIndex)

            case .removeRange(let from, let count):
                let startClamped = max(0, min(from, stem.count))
                let endClamped = min(startClamped + count, stem.count)
                if startClamped < endClamped {
                    let start = stem.index(stem.startIndex, offsetBy: startClamped)
                    let end = stem.index(stem.startIndex, offsetBy: endClamped)
                    stem.removeSubrange(start..<end)
                }

            case .changeExtension(let newExt):
                extensionOverride = newExt

            case .numberSequence, .dateBased:
                // Implemented in Task 5
                break
            }
        }

        let finalExt = extensionOverride ?? ext
        if finalExt.isEmpty {
            return stem
        } else {
            return "\(stem).\(finalExt)"
        }
    }

    private static func applyRegex(pattern: String, replacement: String, to string: String) -> String {
        guard let regex = try? NSRegularExpression(pattern: pattern) else { return string }
        let range = NSRange(string.startIndex..., in: string)
        return regex.stringByReplacingMatches(in: string, range: range, withTemplate: replacement)
    }

    private static func applyCase(_ style: CaseStyle, to string: String) -> String {
        switch style {
        case .lower:
            return string.lowercased()
        case .upper:
            return string.uppercased()
        case .title:
            return string.capitalized
        case .camel:
            let words = string.components(separatedBy: CharacterSet.alphanumerics.inverted)
                              .filter { !$0.isEmpty }
            guard !words.isEmpty else { return string }
            let first = words[0].lowercased()
            let rest = words.dropFirst().map { $0.capitalized }
            return ([first] + rest).joined()
        }
    }
}
