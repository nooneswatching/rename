import Foundation

enum NumberPosition: String, CaseIterable {
    case prefix = "Prefix"
    case suffix = "Suffix"
    case replaceAll = "Replace All"
}

enum CaseStyle: String, CaseIterable {
    case lower = "lowercase"
    case upper = "UPPERCASE"
    case title = "Title Case"
    case camel = "camelCase"
}

enum DateSource: String, CaseIterable {
    case fileModified = "File Modified"
    case fileCreated = "File Created"
    case today = "Today"
}

enum RenameRule: Equatable {
    case prefix(String)
    case suffix(String)
    case findReplace(find: String, replace: String, caseSensitive: Bool)
    case regexFindReplace(pattern: String, replacement: String)
    case numberSequence(start: Int, step: Int, digits: Int, position: NumberPosition)
    case changeCase(CaseStyle)
    case insertAt(text: String, index: Int)
    case removeRange(from: Int, count: Int)
    case changeExtension(String)
    case dateBased(format: String, source: DateSource)

    var displayName: String {
        switch self {
        case .prefix: return "Prefix"
        case .suffix: return "Suffix"
        case .findReplace: return "Find & Replace"
        case .regexFindReplace: return "Regex Find & Replace"
        case .numberSequence: return "Numbering"
        case .changeCase: return "Change Case"
        case .insertAt: return "Insert at Position"
        case .removeRange: return "Remove Range"
        case .changeExtension: return "Change Extension"
        case .dateBased: return "Date-based Naming"
        }
    }
}

struct RenameRuleItem: Identifiable {
    let id: UUID
    var rule: RenameRule
    var isEnabled: Bool

    init(rule: RenameRule, isEnabled: Bool = true) {
        self.id = UUID()
        self.rule = rule
        self.isEnabled = isEnabled
    }
}
