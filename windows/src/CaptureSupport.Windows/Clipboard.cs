using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Screenshot.Support.Windows;

public sealed class InvalidImageException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>What goes on the clipboard for one image (CLP-001): the original PNG bytes and a 32-bit DIBV5.</summary>
public sealed record ClipboardPayload(byte[] Png, byte[] DibV5, int Width, int Height)
{
    /// <summary>Prepares every format first. Throws <see cref="InvalidImageException"/> without touching anything (CLP-002).</summary>
    public static ClipboardPayload Create(byte[] image)
    {
        BitmapFrame frame;
        try { frame = WicImageDecoder.DecodeFirstFrame(image); }
        catch (Exception e) when (WicImageDecoder.IsDecodeFailure(e)) { throw new InvalidImageException("The image can't be read.", e); }
        if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0) throw new InvalidImageException("The image is empty.");
        var bgra = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        return new ClipboardPayload(image, DibV5Format.Build(bgra), frame.PixelWidth, frame.PixelHeight);
    }
}

/// <summary>Builds a bottom-up 32-bit BITMAPV5HEADER + pixels, the form most Windows apps paste best.</summary>
public static class DibV5Format
{
    public const int HeaderSize = 124;

    public static byte[] Build(BitmapSource bgra32)
    {
        int w = bgra32.PixelWidth, h = bgra32.PixelHeight, stride = w * 4;
        var pixels = new byte[stride * h];
        bgra32.CopyPixels(pixels, stride, 0);

        var dib = new byte[HeaderSize + pixels.Length];
        var s = dib.AsSpan();
        BinaryPrimitives.WriteInt32LittleEndian(s[0..], HeaderSize);   // bV5Size
        BinaryPrimitives.WriteInt32LittleEndian(s[4..], w);            // bV5Width
        BinaryPrimitives.WriteInt32LittleEndian(s[8..], h);            // bV5Height (positive = bottom-up)
        BinaryPrimitives.WriteInt16LittleEndian(s[12..], 1);           // bV5Planes
        BinaryPrimitives.WriteInt16LittleEndian(s[14..], 32);          // bV5BitCount
        BinaryPrimitives.WriteInt32LittleEndian(s[16..], 3);           // bV5Compression = BI_BITFIELDS
        BinaryPrimitives.WriteInt32LittleEndian(s[20..], pixels.Length); // bV5SizeImage
        BinaryPrimitives.WriteInt32LittleEndian(s[24..], 3780);        // ~96 dpi in pixels per metre
        BinaryPrimitives.WriteInt32LittleEndian(s[28..], 3780);
        BinaryPrimitives.WriteUInt32LittleEndian(s[40..], 0x00FF0000); // red mask
        BinaryPrimitives.WriteUInt32LittleEndian(s[44..], 0x0000FF00); // green mask
        BinaryPrimitives.WriteUInt32LittleEndian(s[48..], 0x000000FF); // blue mask
        BinaryPrimitives.WriteUInt32LittleEndian(s[52..], 0xFF000000); // alpha mask
        BinaryPrimitives.WriteUInt32LittleEndian(s[56..], 0x73524742); // bV5CSType = LCS_sRGB
        BinaryPrimitives.WriteInt32LittleEndian(s[108..], 4);          // bV5Intent = LCS_GM_IMAGES

        // Flip rows: DIB rows run bottom to top.
        for (var y = 0; y < h; y++)
            pixels.AsSpan(y * stride, stride).CopyTo(s[(HeaderSize + (h - 1 - y) * stride)..]);
        return dib;
    }

    public static (int Width, int Height) ReadSize(byte[] dib) =>
        (BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(4)), Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(8))));
}

/// <summary>Where a prepared payload is written. Production writes the Windows clipboard; tests use a fake.</summary>
public interface IClipboardSink
{
    void Write(ClipboardPayload payload);
}

/// <summary>Copies an image: prepare everything, then write once (CLP-001 to CLP-003).</summary>
public sealed class ImageCopier(IClipboardSink sink) : IImageCopier
{
    public void Copy(byte[] image) => sink.Write(ClipboardPayload.Create(image));
}

public sealed class ClipboardException(string message) : Exception(message);

/// <summary>The real Windows clipboard: "PNG" + CF_DIBV5 in one update. Retries while another app holds it.</summary>
public sealed partial class Win32Clipboard(nint ownerWindow) : IClipboardSink
{
    private const uint CF_DIBV5 = 17;
    private const uint GMEM_MOVEABLE = 0x0002;
    public const int Attempts = 10;
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    public void Write(ClipboardPayload payload)
    {
        var pngFormat = RegisterClipboardFormatW("PNG");
        if (pngFormat == 0) throw new ClipboardException("Windows couldn't register the PNG clipboard format.");

        var opened = false;
        for (var i = 0; i < Attempts && !opened; i++)
        {
            opened = OpenClipboard(ownerWindow);
            if (!opened) Thread.Sleep(RetryDelay);
        }
        if (!opened) throw new ClipboardException("Another app is using the clipboard. Try again in a moment.");

        try
        {
            // CLP-003: cleared right before writing; not restored if the write fails.
            if (!EmptyClipboard()) throw new ClipboardException($"Windows couldn't clear the clipboard (error {Marshal.GetLastPInvokeError()}).");
            Put(pngFormat, payload.Png);
            Put(CF_DIBV5, payload.DibV5);
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static void Put(uint format, byte[] data)
    {
        var handle = GlobalAlloc(GMEM_MOVEABLE, (nuint)data.Length);
        if (handle == 0) throw new ClipboardException("Not enough memory to copy the image.");
        var ok = false;
        try
        {
            var p = GlobalLock(handle);
            if (p == 0) throw new ClipboardException("Not enough memory to copy the image.");
            try { Marshal.Copy(data, 0, p, data.Length); }
            finally { GlobalUnlock(handle); }
            if (SetClipboardData(format, handle) == 0)
                throw new ClipboardException($"Windows didn't accept the image on the clipboard (error {Marshal.GetLastPInvokeError()}).");
            ok = true; // the clipboard owns the memory now
        }
        finally
        {
            if (!ok) GlobalFree(handle);
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial uint RegisterClipboardFormatW(string name);
    [LibraryImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(nint owner);
    [LibraryImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();
    [LibraryImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();
    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint SetClipboardData(uint format, nint handle);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GlobalAlloc(uint flags, nuint bytes);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GlobalLock(nint handle);
    [LibraryImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint handle);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GlobalFree(nint handle);
}
