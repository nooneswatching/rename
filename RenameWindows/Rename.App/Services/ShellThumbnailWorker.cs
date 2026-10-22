using System.Collections.Concurrent;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Rename.App.Services;

/// <summary>
/// Asks the Windows shell for a file's thumbnail (or its icon when no thumbnail provider exists) through
/// IShellItemImageFactory. Shell thumbnail handlers expect a single-threaded apartment, so all requests are
/// serviced on one dedicated STA thread. Falls back to the small associated icon if the shell call fails.
/// </summary>
internal sealed class ShellThumbnailWorker
{
    private readonly BlockingCollection<(string Path, int Size, TaskCompletionSource<ImageSource?> Result)> _queue = new();

    public ShellThumbnailWorker()
    {
        var thread = new Thread(Run) { IsBackground = true, Name = "Shell thumbnails" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public Task<ImageSource?> GetImageAsync(string path, int size, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<ImageSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        _queue.Add((path, size, tcs), CancellationToken.None);
        return tcs.Task;
    }

    private void Run()
    {
        foreach (var (path, size, result) in _queue.GetConsumingEnumerable())
        {
            if (result.Task.IsCompleted) continue;
            ImageSource? image = null;
            try { image = ShellImage(path, size) ?? AssociatedIcon(path); }
            catch (Exception) { /* leave the placeholder */ }
            result.TrySetResult(image);
        }
    }

    // MARK: IShellItemImageFactory

    private static ImageSource? ShellImage(string path, int size)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        var hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory);
        if (hr != 0 || factory is null) return null;
        try
        {
            // Thumbnail when a provider exists (photos, videos, PDFs…), otherwise the file-type icon.
            hr = factory.GetImage(new SIZE(size, size), SIIGBF.BiggerSizeOk | SIIGBF.ResizeToFit, out var hBitmap);
            if (hr != 0 || hBitmap == IntPtr.Zero) return null;
            try { return FromHBitmapWithAlpha(hBitmap); }
            finally { DeleteObject(hBitmap); }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    /// <summary>
    /// Imaging.CreateBitmapSourceFromHBitmap drops the alpha channel, which turns icon backgrounds black.
    /// Reading the raw 32-bit pixels through GDI+ keeps transparency intact.
    /// </summary>
    private static BitmapSource FromHBitmapWithAlpha(IntPtr hBitmap)
    {
        using var gdi = System.Drawing.Image.FromHbitmap(hBitmap);
        var width = gdi.Width;
        var height = gdi.Height;
        var bounds = new System.Drawing.Rectangle(0, 0, width, height);

        if (System.Drawing.Image.GetPixelFormatSize(gdi.PixelFormat) != 32)
        {
            var opaque = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            opaque.Freeze();
            return opaque;
        }

        var data = gdi.LockBits(bounds, ImageLockMode.ReadOnly, gdi.PixelFormat);
        try
        {
            var stride = Math.Abs(data.Stride);
            var pixels = new byte[stride * height];
            for (var row = 0; row < height; row++)
            {
                Marshal.Copy(data.Scan0 + row * data.Stride, pixels, row * stride, stride);
            }

            // A 32-bit DIB with every alpha byte at zero carries no transparency and must be shown opaque.
            var hasAlpha = false;
            for (var i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] != 0) { hasAlpha = true; break; }
            }

            var format = hasAlpha ? PixelFormats.Pbgra32 : PixelFormats.Bgr32;
            var source = BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
            source.Freeze();
            return source;
        }
        finally
        {
            gdi.UnlockBits(data);
        }
    }

    // MARK: Fallback: SHGetFileInfo icon

    private static ImageSource? AssociatedIcon(string path)
    {
        var info = new SHFILEINFO();
        var result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    // MARK: Interop

    [StructLayout(LayoutKind.Sequential)]
    internal struct SIZE(int cx, int cy)
    {
        public int cx = cx;
        public int cy = cy;
    }

    [Flags]
    internal enum SIIGBF
    {
        ResizeToFit = 0x00,
        BiggerSizeOk = 0x01,
        MemoryOnly = 0x02,
        IconOnly = 0x04,
        ThumbnailOnly = 0x08,
        InCacheOnly = 0x10,
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItemImageFactory? ppv);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);
}
