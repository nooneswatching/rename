import SwiftUI

struct SidebarView: View {
    @EnvironmentObject var appState: AppState
    @State private var extensionFilter: String = ""

    private var sourceSummary: String {
        let dirs = Set(appState.files.map { $0.originalURL.deletingLastPathComponent().path })
        switch dirs.count {
        case 0: return "No files loaded"
        case 1: return dirs.first.map { URL(fileURLWithPath: $0).lastPathComponent } ?? "—"
        default: return "Multiple sources"
        }
    }

    private var availableExtensions: [String] {
        let exts = Set(appState.files.map { $0.originalURL.pathExtension.lowercased() })
            .filter { !$0.isEmpty }
        return exts.sorted()
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            VStack(alignment: .leading, spacing: 4) {
                Text("Source")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                Text(sourceSummary)
                    .font(.body)
                    .lineLimit(2)
                    .truncationMode(.middle)
                Text("\(appState.files.count) file\(appState.files.count == 1 ? "" : "s")")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            .padding(.horizontal)
            .padding(.top)

            Divider()

            if !availableExtensions.isEmpty {
                VStack(alignment: .leading, spacing: 6) {
                    Text("Filter by type")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .padding(.horizontal)

                    ScrollView {
                        VStack(alignment: .leading, spacing: 2) {
                            FilterRow(label: "All", isSelected: extensionFilter.isEmpty) {
                                extensionFilter = ""
                            }
                            ForEach(availableExtensions, id: \.self) { ext in
                                FilterRow(label: ".\(ext)", isSelected: extensionFilter == ext) {
                                    extensionFilter = ext
                                }
                            }
                        }
                        .padding(.horizontal, 8)
                    }
                }
            }

            Spacer()

            Button(role: .destructive, action: { appState.clearFiles() }) {
                Label("Clear All", systemImage: "trash")
                    .frame(maxWidth: .infinity)
            }
            .padding()
            .disabled(appState.files.isEmpty)
        }
        .onChange(of: extensionFilter) { _, newValue in
            appState.extensionFilter = newValue
        }
    }
}

private struct FilterRow: View {
    let label: String
    let isSelected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Text(label)
                .font(.caption)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.vertical, 3)
                .padding(.horizontal, 6)
                .background(isSelected ? Color.accentColor.opacity(0.15) : Color.clear)
                .cornerRadius(4)
        }
        .buttonStyle(.plain)
    }
}
