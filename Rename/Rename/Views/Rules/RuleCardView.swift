import SwiftUI

struct RuleCardView: View {
    @Binding var item: RenameRuleItem
    let onDelete: () -> Void
    @State private var isExpanded = true

    var body: some View {
        VStack(spacing: 0) {
            // Header
            HStack {
                Toggle("", isOn: $item.isEnabled)
                    .labelsHidden()
                    .toggleStyle(.checkbox)

                Text(item.rule.displayName)
                    .font(.subheadline)
                    .fontWeight(.medium)

                Spacer()

                Button(action: { withAnimation { isExpanded.toggle() } }) {
                    Image(systemName: isExpanded ? "chevron.up" : "chevron.down")
                        .font(.caption)
                }
                .buttonStyle(.plain)

                Button(action: onDelete) {
                    Image(systemName: "xmark")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 8)

            if isExpanded {
                Divider()
                ruleControls
                    .padding(10)
            }
        }
        .background(Color(NSColor.controlBackgroundColor))
        .clipShape(RoundedRectangle(cornerRadius: 8))
        .overlay(
            RoundedRectangle(cornerRadius: 8)
                .stroke(Color(NSColor.separatorColor), lineWidth: 1)
        )
    }

    @ViewBuilder
    private var ruleControls: some View {
        switch item.rule {
        case .prefix(let text):
            LabeledTextField("Text", value: text) { item.rule = .prefix($0) }

        case .suffix(let text):
            LabeledTextField("Text", value: text) { item.rule = .suffix($0) }

        case .findReplace(let find, let replace, let cs):
            VStack(spacing: 6) {
                LabeledTextField("Find", value: find) { item.rule = .findReplace(find: $0, replace: replace, caseSensitive: cs) }
                LabeledTextField("Replace", value: replace) { item.rule = .findReplace(find: find, replace: $0, caseSensitive: cs) }
                Toggle("Case sensitive", isOn: Binding(get: { cs }, set: { item.rule = .findReplace(find: find, replace: replace, caseSensitive: $0) }))
                    .font(.caption)
            }

        case .regexFindReplace(let pattern, let replacement):
            VStack(spacing: 6) {
                LabeledTextField("Pattern", value: pattern) {
                    item.rule = .regexFindReplace(pattern: $0, replacement: replacement)
                }
                if !pattern.isEmpty, (try? NSRegularExpression(pattern: pattern)) == nil {
                    Text("Invalid regex pattern")
                        .font(.caption)
                        .foregroundStyle(.red)
                }
                LabeledTextField("Replacement", value: replacement) {
                    item.rule = .regexFindReplace(pattern: pattern, replacement: $0)
                }
            }

        case .numberSequence(let start, let step, let digits, let position):
            VStack(spacing: 6) {
                LabeledIntField("Start", value: start) { item.rule = .numberSequence(start: $0, step: step, digits: digits, position: position) }
                LabeledIntField("Step", value: step) { item.rule = .numberSequence(start: start, step: $0, digits: digits, position: position) }
                LabeledIntField("Digits (min)", value: digits) { item.rule = .numberSequence(start: start, step: step, digits: $0, position: position) }
                Picker("Position", selection: Binding(get: { position }, set: { item.rule = .numberSequence(start: start, step: step, digits: digits, position: $0) })) {
                    ForEach(NumberPosition.allCases, id: \.self) { Text($0.rawValue).tag($0) }
                }
                .font(.caption)
            }

        case .changeCase(let style):
            Picker("Case", selection: Binding(get: { style }, set: { item.rule = .changeCase($0) })) {
                ForEach(CaseStyle.allCases, id: \.self) { Text($0.rawValue).tag($0) }
            }

        case .insertAt(let text, let index):
            VStack(spacing: 6) {
                LabeledTextField("Text", value: text) { item.rule = .insertAt(text: $0, index: index) }
                LabeledIntField("At position", value: index) { item.rule = .insertAt(text: text, index: $0) }
            }

        case .removeRange(let from, let count):
            VStack(spacing: 6) {
                LabeledIntField("From position", value: from) { item.rule = .removeRange(from: $0, count: count) }
                LabeledIntField("Character count", value: count) { item.rule = .removeRange(from: from, count: $0) }
            }

        case .changeExtension(let ext):
            LabeledTextField("Extension", value: ext) { item.rule = .changeExtension($0) }

        case .dateBased(let format, let source):
            VStack(spacing: 6) {
                LabeledTextField("Format (e.g. yyyy-MM-dd)", value: format) { item.rule = .dateBased(format: $0, source: source) }
                Picker("Date source", selection: Binding(get: { source }, set: { item.rule = .dateBased(format: format, source: $0) })) {
                    ForEach(DateSource.allCases, id: \.self) { Text($0.rawValue).tag($0) }
                }
                .font(.caption)
            }
        }
    }
}

// MARK: - Helpers

private struct LabeledTextField: View {
    let label: String
    let value: String
    let onChange: (String) -> Void
    @State private var text: String

    init(_ label: String, value: String, onChange: @escaping (String) -> Void) {
        self.label = label
        self.value = value
        self.onChange = onChange
        self._text = State(initialValue: value)
    }

    var body: some View {
        HStack {
            Text(label)
                .font(.caption)
                .foregroundStyle(.secondary)
                .frame(width: 80, alignment: .leading)
            TextField("", text: $text)
                .textFieldStyle(.roundedBorder)
                .font(.caption)
                .onChange(of: text) { _, newValue in onChange(newValue) }
        }
    }
}

private struct LabeledIntField: View {
    let label: String
    let value: Int
    let onChange: (Int) -> Void
    @State private var text: String

    init(_ label: String, value: Int, onChange: @escaping (Int) -> Void) {
        self.label = label
        self.value = value
        self.onChange = onChange
        self._text = State(initialValue: "\(value)")
    }

    var body: some View {
        HStack {
            Text(label)
                .font(.caption)
                .foregroundStyle(.secondary)
                .frame(width: 80, alignment: .leading)
            TextField("", text: $text)
                .textFieldStyle(.roundedBorder)
                .font(.caption)
                .onChange(of: text) { _, newValue in
                    if let n = Int(newValue) { onChange(n) }
                }
        }
    }
}
