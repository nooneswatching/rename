import SwiftUI

struct FileRowView: View {
    let file: RenameFile
    let isSelected: Bool
    let isConflict: Bool
    let onSelect: () -> Void

    var body: some View {
        HStack(spacing: 10) {
            Image(systemName: "line.3.horizontal")
                .foregroundStyle(.tertiary)
                .frame(width: 20)

            if let thumbnail = file.thumbnail {
                Image(nsImage: thumbnail)
                    .resizable()
                    .aspectRatio(contentMode: .fit)
                    .frame(width: 20, height: 20)
            } else {
                Image(systemName: "doc")
                    .frame(width: 20, height: 20)
            }

            Text(file.originalURL.lastPathComponent)
                .lineLimit(1)
                .truncationMode(.middle)
                .foregroundStyle(.primary)
                .frame(maxWidth: .infinity, alignment: .leading)

            Image(systemName: "arrow.right")
                .foregroundStyle(.tertiary)
                .font(.caption)

            Text(file.computedName)
                .lineLimit(1)
                .truncationMode(.middle)
                .foregroundStyle(isConflict ? .red : .accentColor)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(.vertical, 4)
        .padding(.horizontal, 8)
        .background(isSelected ? Color.accentColor.opacity(0.1) : Color.clear)
        .cornerRadius(6)
        .contentShape(Rectangle())
        .onTapGesture { onSelect() }
    }
}
