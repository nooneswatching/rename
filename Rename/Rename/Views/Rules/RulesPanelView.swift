import SwiftUI

struct RulesPanelView: View {
    @EnvironmentObject var appState: AppState
    @State private var showAddMenu = false

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text("Rename Rules")
                    .font(.headline)
                Spacer()
                Button(action: { showAddMenu = true }) {
                    Image(systemName: "plus")
                }
                .buttonStyle(.plain)
                .popover(isPresented: $showAddMenu) {
                    AddRuleMenu(isPresented: $showAddMenu)
                        .environmentObject(appState)
                }
            }
            .padding(.horizontal, 12)
            .padding(.vertical, 10)

            Divider()

            if appState.rules.isEmpty {
                VStack(spacing: 8) {
                    Image(systemName: "wand.and.stars")
                        .font(.title2)
                        .foregroundStyle(.tertiary)
                    Text("Add a rule to start renaming")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                List {
                    ForEach($appState.rules) { $item in
                        RuleCardView(item: $item) {
                            appState.removeRule(id: item.id)
                        }
                        .listRowSeparator(.hidden)
                        .listRowInsets(EdgeInsets(top: 4, leading: 8, bottom: 4, trailing: 8))
                    }
                    .onMove { source, destination in
                        appState.moveRules(from: source, to: destination)
                    }
                }
                .listStyle(.plain)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(Color(NSColor.windowBackgroundColor))
    }
}

// MARK: - Add Rule Menu

private struct AddRuleMenu: View {
    @EnvironmentObject var appState: AppState
    @Binding var isPresented: Bool

    private let ruleTemplates: [(String, RenameRule)] = [
        ("Prefix", .prefix("")),
        ("Suffix", .suffix("")),
        ("Find & Replace", .findReplace(find: "", replace: "", caseSensitive: true)),
        ("Regex Find & Replace", .regexFindReplace(pattern: "", replacement: "")),
        ("Numbering", .numberSequence(start: 1, step: 1, digits: 2, position: .prefix)),
        ("Change Case", .changeCase(.lower)),
        ("Insert at Position", .insertAt(text: "", index: 0)),
        ("Remove Range", .removeRange(from: 0, count: 1)),
        ("Change Extension", .changeExtension("")),
        ("Date-based Naming", .dateBased(format: "yyyy-MM-dd", source: .fileModified)),
        ("Custom Name", .template(format: "", numberStart: 1, numberDigits: 2)),
    ]

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text("Add Rule")
                .font(.caption)
                .foregroundStyle(.secondary)
                .padding(.horizontal, 12)
                .padding(.top, 8)

            ForEach(ruleTemplates, id: \.0) { name, rule in
                Button(action: {
                    appState.addRule(rule)
                    isPresented = false
                }) {
                    Text(name)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.horizontal, 12)
                        .padding(.vertical, 5)
                }
                .buttonStyle(.plain)
                .contentShape(Rectangle())
            }
        }
        .padding(.bottom, 8)
        .frame(width: 200)
    }
}
