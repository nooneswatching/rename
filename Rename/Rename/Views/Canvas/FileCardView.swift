import SwiftUI

struct FileCardView: View {
    let file: RenameFile
    let isSelected: Bool
    let isConflict: Bool
    let isDragging: Bool
    let dropIndicator: DropIndicator?
    let thumbnailSize: CGFloat
    let onSelect: () -> Void

    private var thumbnailWidth: CGFloat { thumbnailSize * 1.25 }

    var body: some View {
        VStack(spacing: 4) {
            ZStack {
                RoundedRectangle(cornerRadius: 8)
                    .fill(Color(NSColor.controlBackgroundColor))
                    .frame(width: thumbnailWidth, height: thumbnailSize)

                if let thumbnail = file.thumbnail {
                    Image(nsImage: thumbnail)
                        .resizable()
                        .aspectRatio(contentMode: .fit)
                        .frame(width: thumbnailWidth, height: thumbnailSize)
                        .clipShape(RoundedRectangle(cornerRadius: 8))
                } else {
                    ProgressView()
                        .frame(width: thumbnailWidth, height: thumbnailSize)
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
                .frame(width: thumbnailWidth)
                .foregroundStyle(.secondary)

            Text(file.computedName)
                .font(.caption2)
                .lineLimit(2)
                .truncationMode(.middle)
                .frame(width: thumbnailWidth)
                .foregroundStyle(isConflict ? .red : .accentColor)
        }
        .padding(4)
        .background(
            RoundedRectangle(cornerRadius: 10)
                .fill(isSelected ? Color.accentColor.opacity(0.08) : Color.clear)
        )
        .opacity(isDragging ? 0.4 : 1.0)
        .overlay(alignment: .leading) {
            if dropIndicator == .leading {
                Capsule()
                    .fill(Color.accentColor)
                    .frame(width: 2)
                    .padding(.vertical, 4)
            }
        }
        .overlay(alignment: .trailing) {
            if dropIndicator == .trailing {
                Capsule()
                    .fill(Color.accentColor)
                    .frame(width: 2)
                    .padding(.vertical, 4)
            }
        }
        .onTapGesture { onSelect() }
    }
}
