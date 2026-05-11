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
        let newSize = CGSize(width: floor(original.width * scale), height: floor(original.height * scale))

        guard let cgImage = image.cgImage(forProposedRect: nil, context: nil, hints: nil) else { return image }
        guard let ctx = CGContext(
            data: nil,
            width: Int(newSize.width),
            height: Int(newSize.height),
            bitsPerComponent: 8,
            bytesPerRow: Int(newSize.width) * 4,
            space: CGColorSpaceCreateDeviceRGB(),
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        ) else { return image }

        ctx.draw(cgImage, in: CGRect(origin: .zero, size: newSize))
        guard let outputCG = ctx.makeImage() else { return image }
        return NSImage(cgImage: outputCG, size: newSize)
    }
}
