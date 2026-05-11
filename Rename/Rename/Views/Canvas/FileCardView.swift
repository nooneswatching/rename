import SwiftUI

struct FileCardView: View {
    let file: RenameFile
    let isSelected: Bool
    let isConflict: Bool
    let onSelect: () -> Void

    var body: some View {
        VStack(spacing: 4) {
            ZStack {
                RoundedRectangle(cornerRadius: 8)
                    .fill(Color(NSColor.controlBackgroundColor))
                    .frame(width: 100, height: 80)

                if let thumbnail = file.thumbnail {
                    Image(nsImage: thumbnail)
                        .resizable()
                        .aspectRatio(contentMode: .fit)
                        .frame(width: 100, height: 80)
                        .clipShape(RoundedRectangle(cornerRadius: 8))
                } else {
                    ProgressView()
                        .frame(width: 100, height: 80)
                }
            }
            .overlay(
                RoundedRectangle(cornerRadius: 8)
                    .stroke(isSelected ? Color.accentColor : Color.clear, lineWidth: 2)
            )

            Text(file.originalURL.lastPathComponent)
                .font(.caption2)
                .lineLimit(2)
                .truncationMode(.middle)
                .frame(width: 100)
                .foregroundStyle(.secondary)

            Text(file.computedName)
                .font(.caption2)
                .lineLimit(2)
                .truncationMode(.middle)
                .frame(width: 100)
                .foregroundStyle(isConflict ? .red : .accentColor)
        }
        .padding(4)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(isSelected ? Color.accentColor.opacity(0.08) : Color.clear)
        )
        .onTapGesture { onSelect() }
    }
}
