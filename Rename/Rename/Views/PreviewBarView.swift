import SwiftUI

struct PreviewBarView: View {
    @EnvironmentObject var appState: AppState
    let onApply: () -> Void

    private var previewFile: RenameFile? {
        if appState.selectedFileIDs.count == 1,
           let id = appState.selectedFileIDs.first {
            return appState.files.first(where: { $0.id == id })
        }
        return appState.files.first
    }

    var body: some View {
        HStack(spacing: 16) {
            if let file = previewFile {
                Text(file.originalURL.lastPathComponent)
                    .lineLimit(1)
                    .truncationMode(.middle)
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .trailing)

                Image(systemName: "arrow.right")
                    .foregroundStyle(.tertiary)

                Text(file.computedName)
                    .lineLimit(1)
                    .truncationMode(.middle)
                    .foregroundStyle(appState.conflictedNames.contains(file.computedName) ? .red : .accentColor)
                    .frame(maxWidth: .infinity, alignment: .leading)
            } else {
                Text("No files loaded")
                    .foregroundStyle(.tertiary)
                    .frame(maxWidth: .infinity)
            }

            Button("Apply Rename") {
                onApply()
            }
            .buttonStyle(.borderedProminent)
            .disabled(appState.files.isEmpty || appState.hasConflicts)
            .help(appState.hasConflicts ? "Resolve naming conflicts before applying" : "")
        }
        .padding(.horizontal, 16)
    }
}
