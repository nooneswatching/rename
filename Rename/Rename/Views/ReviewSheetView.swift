import SwiftUI

struct ReviewSheetView: View {
    @EnvironmentObject var appState: AppState
    @Binding var isPresented: Bool

    private var conflicts: Set<String> { appState.conflictedNames }

    var body: some View {
        VStack(spacing: 0) {
            // Header
            HStack {
                Text("Review Renames")
                    .font(.headline)
                Spacer()
                if !conflicts.isEmpty {
                    Label("\(conflicts.count) conflict\(conflicts.count == 1 ? "" : "s")", systemImage: "exclamationmark.triangle.fill")
                        .font(.caption)
                        .foregroundStyle(.red)
                }
            }
            .padding()

            Divider()

            // Column headers
            HStack {
                Text("Original Name")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
                Text("New Name")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .padding(.horizontal)
            .padding(.vertical, 6)
            .background(Color(NSColor.controlBackgroundColor))

            Divider()

            // File list
            ScrollView {
                LazyVStack(spacing: 0) {
                    ForEach(Array(appState.files.enumerated()), id: \.element.id) { index, file in
                        HStack {
                            Text(file.originalURL.lastPathComponent)
                                .lineLimit(1)
                                .truncationMode(.middle)
                                .frame(maxWidth: .infinity, alignment: .leading)
                            Text(file.computedName)
                                .lineLimit(1)
                                .truncationMode(.middle)
                                .foregroundStyle(conflicts.contains(file.computedName) ? .red : .primary)
                                .frame(maxWidth: .infinity, alignment: .leading)
                        }
                        .padding(.horizontal)
                        .padding(.vertical, 5)
                        .background(index % 2 == 0 ? Color.clear : Color(NSColor.controlBackgroundColor).opacity(0.4))

                        if index < appState.files.count - 1 {
                            Divider().padding(.horizontal)
                        }
                    }
                }
            }

            Divider()

            // Action buttons
            HStack {
                Button("Cancel") {
                    isPresented = false
                }
                .keyboardShortcut(.escape)

                Spacer()

                if !conflicts.isEmpty {
                    Text("Fix conflicts before confirming")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                Button("Confirm Rename") {
                    isPresented = false
                    appState.applyRename()
                }
                .buttonStyle(.borderedProminent)
                .keyboardShortcut(.return)
                .disabled(!conflicts.isEmpty)
            }
            .padding()
        }
        .frame(width: 600, height: 500)
    }
}
