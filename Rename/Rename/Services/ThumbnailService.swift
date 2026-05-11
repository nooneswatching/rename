import AppKit

actor ThumbnailService {
    static let shared = ThumbnailService()

    private var cache: [URL: NSImage] = [:]
    private let imageExtensions: Set<String> = ["jpg", "jpeg", "png", "gif", "heic", "heif", "tiff", "tif", "webp", "bmp"]
    private let targetSize = CGSize(width: 120, height: 120)

    func thumbnail(for url: URL) async -> NSImage {
        if let cached = cache[url] { return cached }
        let image = await loadImage(for: url)
        cache[url] = image
        return image
    }

    func clearCache() {
        cache.removeAll()
    }

    // MARK: Private

    private func loadImage(for url: URL) async -> NSImage {
        let ext = url.pathExtension.lowercased()
        if imageExtensions.contains(ext) {
            return await Task.detached(priority: .userInitiated) { [targetSize] in
                guard let src = NSImage(contentsOf: url) else {
                    return NSWorkspace.shared.icon(forFile: url.path)
                }
                return ThumbnailService.downscale(src, to: targetSize)
            }.value
        } else {
            return await Task.detached(priority: .userInitiated) {
                NSWorkspace.shared.icon(forFile: url.path)
            }.value
        }
    }

    private static func downscale(_ image: NSImage, to targetSize: CGSize) -> NSImage {
        let original = image.size
        guard original.width > 0, original.height > 0 else { return image }
        let scale = min(targetSize.width / original.width, targetSize.height / original.height)
        let newSize = CGSize(width: original.width * scale, height: original.height * scale)
        let result = NSImage(size: newSize)
        result.lockFocus()
        image.draw(in: CGRect(origin: .zero, size: newSize))
        result.unlockFocus()
        return result
    }
}
