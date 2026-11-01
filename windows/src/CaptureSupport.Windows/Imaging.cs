using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Screenshot.Support.Windows;

/// <summary>Decodes with WIC (Windows Imaging Component) through WPF.</summary>
public sealed class WicImageDecoder : IImageDecoder
{
    public PixelSize? TryDecodeSize(byte[] data)
    {
        try
        {
            var frame = DecodeFirstFrame(data);
            // Touch the pixels so a damaged file fails here, not later.
            var stride = Math.Max(1, (frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8);
            frame.CopyPixels(new System.Windows.Int32Rect(0, 0, Math.Min(1, frame.PixelWidth), Math.Min(1, frame.PixelHeight)), new byte[stride], stride, 0);
            return frame.PixelWidth > 0 && frame.PixelHeight > 0 ? new PixelSize(frame.PixelWidth, frame.PixelHeight) : null;
        }
        catch (Exception e) when (IsDecodeFailure(e))
        {
            return null;
        }
    }

    internal static BitmapFrame DecodeFirstFrame(byte[] data)
    {
        using var ms = new MemoryStream(data, writable: false);
        var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0) throw new FileFormatException("The image has no frames.");
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    internal static bool IsDecodeFailure(Exception e) =>
        e is NotSupportedException or FileFormatException or ArgumentException or IOException
            or InvalidOperationException or System.Runtime.InteropServices.COMException or OverflowException;
}

/// <summary>Small, bounded previews (PRE-004). The original file is only read, never changed.</summary>
public static class Thumbnailer
{
    public const int DefaultMaxPixels = 640;

    /// <summary>A frozen thumbnail whose longest side is at most <paramref name="maxPixels"/>, or null.</summary>
    public static BitmapSource? Load(byte[] data, int maxPixels = DefaultMaxPixels)
    {
        if (maxPixels <= 0) return null;
        try
        {
            // Read the size first without decoding pixels.
            int w, h;
            using (var probe = new MemoryStream(data, writable: false))
            {
                var d = BitmapDecoder.Create(probe, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                if (d.Frames.Count == 0) return null;
                w = d.Frames[0].PixelWidth;
                h = d.Frames[0].PixelHeight;
            }
            if (w <= 0 || h <= 0) return null;

            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = new MemoryStream(data, writable: false);
            image.CacheOption = BitmapCacheOption.OnLoad; // decode now, keep only the small copy
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (w >= h) image.DecodePixelWidth = Math.Min(w, maxPixels);
            else image.DecodePixelHeight = Math.Min(h, maxPixels);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (WicImageDecoder.IsDecodeFailure(e))
        {
            return null;
        }
    }

    public static BitmapSource? LoadFile(string path, int maxPixels = DefaultMaxPixels)
    {
        try { return Load(File.ReadAllBytes(path), maxPixels); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }
}

/// <summary>Encodes pixels as PNG (used for captures, which are made in memory).</summary>
public static class PngEncoding
{
    public static byte[] Encode(BitmapSource source, double dpiScale = 1.0)
    {
        var dpi = 96.0 * dpiScale;
        BitmapSource bmp = source;
        if (Math.Abs(source.DpiX - dpi) > 0.01 || Math.Abs(source.DpiY - dpi) > 0.01)
        {
            var stride = source.PixelWidth * ((source.Format.BitsPerPixel + 7) / 8);
            var pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            bmp = BitmapSource.Create(source.PixelWidth, source.PixelHeight, dpi, dpi, source.Format, source.Palette, pixels, stride);
        }
        var encoder = new PngBitmapEncoder { Interlace = PngInterlaceOption.Off };
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    /// <summary>A small gradient image, for fixtures and tests.</summary>
    public static byte[] Generate(int width, int height)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var i = y * stride + x * 4;
                pixels[i] = (byte)(255 * x / Math.Max(1, width - 1));     // B
                pixels[i + 1] = (byte)(255 * y / Math.Max(1, height - 1)); // G
                pixels[i + 2] = 200;                                        // R
                pixels[i + 3] = 255;                                        // A
            }
        return Encode(BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride));
    }
}
