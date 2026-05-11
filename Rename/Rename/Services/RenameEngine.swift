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

            case .numberSequence(let start, let step, let digits, let position):
                let number = start + index * step
                let padded = String(format: "%0\(digits)d", number)
                switch position {
                case .prefix:
                    stem = "\(padded)_\(stem)"
                case .suffix:
                    stem = "\(stem)_\(padded)"
                case .replaceAll:
                    stem = padded
                }

            case .dateBased(let format, let source):
                let date = resolvedDate(for: url, source: source)
                let formatter = DateFormatter()
                formatter.dateFormat = format
                let dateStr = formatter.string(from: date)
                stem = "\(dateStr)_\(stem)"

            case .template(let format, let numberStart, let numberDigits):
                let originalStem = url.deletingPathExtension().lastPathComponent
                let number = numberStart + index
                let padded = String(format: "%0\(max(1, numberDigits))d", number)
                stem = format
                    .replacingOccurrences(of: "{name}", with: originalStem)
                    .replacingOccurrences(of: "{n}", with: padded)
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

    private static func resolvedDate(for url: URL, source: DateSource) -> Date {
        switch source {
        case .today:
            return Date()
        case .fileModified:
            let attrs = try? FileManager.default.attributesOfItem(atPath: url.path)
            return (attrs?[.modificationDate] as? Date) ?? Date()
        case .fileCreated:
            let attrs = try? FileManager.default.attributesOfItem(atPath: url.path)
            return (attrs?[.creationDate] as? Date) ?? Date()
        }
    }
}
