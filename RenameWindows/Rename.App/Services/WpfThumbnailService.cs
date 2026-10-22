using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rename.Core.Services;

namespace Rename.App.Services;

/// <summary>
/// Thumbnails for the canvas. Image files are decoded with WPF's codecs (so HEIC/WebP work when the
/// Windows codec packs are installed); every other file gets the Windows shell thumbnail or icon.
/// Results are frozen so they can be created off the UI thread, and cached by path for the session.
/// </summary>
public sealed class WpfThumbnailService : IThumbnailService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "jpg", "jpeg", "png", "gif", "heic", "heif", "tiff", "tif", "webp", "bmp", "jfif", "avif", "ico",
    };

    /// <summary>Decode target; the largest card the slider allows is 200 px wide.</summary>
    private const int DecodeWidth = 200;
    private const int ShellImageSize = 256;

    private readonly ConcurrentDictionary<string, ImageSource> _cache = new(FileLoader.PathComparer);
    private readonly ShellThumbnailWorker _shell = new();

    public async Task<object?> GetThumbnailAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(path, out var cached)) return cached;

        ImageSource? image = null;
        if (ImageExtensions.Contains(Core.Models.PathParts.Extension(Path.GetFileName(path))))
        {
            image = await Task.Run(() => TryDecodeImage(path), cancellationToken).ConfigureAwait(false);
        }
        image ??= await _shell.GetImageAsync(path, ShellImageSize, cancellationToken).ConfigureAwait(false);

        if (image is null) return null;
        _cache[path] = image;
        return image;
    }

    public void ClearCache() => _cache.Clear();

    private static ImageSource? TryDecodeImage(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            var orientation = ReadOrientation(frame);

            // Re-decode at thumbnail size so a 40 MP photo does not stay in memory.
            stream.Position = 0;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.StreamSource = stream;
            if (frame.PixelWidth >= frame.PixelHeight) bitmap.DecodePixelWidth = Math.Min(DecodeWidth, frame.PixelWidth);
            else bitmap.DecodePixelHeight = Math.Min(DecodeWidth, frame.PixelHeight);
            bitmap.EndInit();

            BitmapSource result = bitmap;
            if (orientation is not null)
            {
                result = new TransformedBitmap(bitmap, orientation);
            }
            result.Freeze();
            return result;
        }
        catch (Exception)
        {
            return null;   // unsupported codec, corrupt file, locked file: fall back to the shell icon
        }
    }

    /// <summary>Maps the EXIF orientation tag to the transform that displays the photo upright.</summary>
    private static Transform? ReadOrientation(BitmapFrame frame)
    {
        try
        {
            if (frame.Metadata is not BitmapMetadata metadata) return null;
            if (!metadata.ContainsQuery("System.Photo.Orientation")) return null;
            var value = metadata.GetQuery("System.Photo.Orientation");
            var orientation = value switch { ushort u => u, short s => (int)s, int i => i, _ => 1 };
            return orientation switch
            {
                2 => new ScaleTransform(-1, 1),
                3 => new RotateTransform(180),
                4 => new ScaleTransform(1, -1),
                5 => new TransformGroup { Children = { new RotateTransform(90), new ScaleTransform(-1, 1) } },
                6 => new RotateTransform(90),
                7 => new TransformGroup { Children = { new RotateTransform(270), new ScaleTransform(-1, 1) } },
                8 => new RotateTransform(270),
                _ => null,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }
}
