using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Screenshot.Support.Windows.Tests;

internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "screenshot-wintests-" + Guid.NewGuid().ToString("N"));
    public TempFolder() => Directory.CreateDirectory(Path);
    public void Dispose() { try { Directory.Delete(Path, true); } catch (IOException) { } }
}

internal sealed class RecordingSink : IClipboardSink
{
    public List<ClipboardPayload> Written { get; } = [];
    public void Write(ClipboardPayload payload) => Written.Add(payload);
}

[TestClass]
public sealed class ImagingTests
{
    private static byte[] Jpeg(int w, int h)
    {
        var src = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr32, null, new byte[w * h * 4], w * 4);
        var enc = new JpegBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(src));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    [STATestMethod]
    public void DecoderReadsSizeAndRejectsJunk()
    {
        var d = new WicImageDecoder();
        Assert.AreEqual(new PixelSize(12, 8), d.TryDecodeSize(PngEncoding.Generate(12, 8)));
        Assert.IsNull(d.TryDecodeSize([1, 2, 3, 4]));
        Assert.IsNull(d.TryDecodeSize([]));
        var png = PngEncoding.Generate(12, 8);
        Assert.IsNull(d.TryDecodeSize(png[..40]), "a truncated PNG must not count as an image");
    }

    [STATestMethod] // T05 with the real decoder
    public void StoreRejectsJpegWithRealDecoder()
    {
        using var t = new TempFolder();
        var e = Assert.ThrowsExactly<CaptureStoreException>(() => new CaptureStore(t.Path, new WicImageDecoder()).Save(Jpeg(4, 4)));
        Assert.AreEqual(CaptureStoreError.NotPng, e.Error);
    }

    [STATestMethod] // T09
    public void CopyPublishesPngAndDibV5OfTheSameSize()
    {
        var png = PngEncoding.Generate(12, 8);
        var sink = new RecordingSink();
        new ImageCopier(sink).Copy(png);
        var p = sink.Written.Single();
        CollectionAssert.AreEqual(png, p.Png);
        Assert.AreEqual((12, 8), DibV5Format.ReadSize(p.DibV5));
        Assert.AreEqual(DibV5Format.HeaderSize + 12 * 8 * 4, p.DibV5.Length);
    }

    [STATestMethod] // DIB rows are bottom-up: the source's top-left pixel is in the last row
    public void DibRowsAreBottomUp()
    {
        var png = PngEncoding.Generate(4, 3); // red 200 everywhere, green grows with y
        var dib = ClipboardPayload.Create(png).DibV5;
        var lastRowFirstPixel = DibV5Format.HeaderSize + 2 * 4 * 4;
        Assert.AreEqual(0, dib[lastRowFirstPixel + 1], "top row (y = 0) has green 0");
        Assert.AreEqual(255, dib[DibV5Format.HeaderSize + 1], "bottom row (y = 2) has green 255");
    }

    [STATestMethod] // T10
    public void InvalidImageNeverReachesTheClipboard()
    {
        var sink = new RecordingSink();
        Assert.ThrowsExactly<InvalidImageException>(() => new ImageCopier(sink).Copy([9, 9, 9]));
        Assert.IsEmpty(sink.Written);
    }

    [STATestMethod] // T15
    public void ThumbnailsAreBounded()
    {
        var png = PngEncoding.Generate(12, 8);
        var copy = (byte[])png.Clone();
        var thumb = Thumbnailer.Load(png, 6)!;
        Assert.AreEqual(6, thumb.PixelWidth);
        Assert.AreEqual(4, thumb.PixelHeight);
        Assert.IsNull(Thumbnailer.Load([1, 2, 3], 6));
        Assert.IsNull(Thumbnailer.Load(png, 0));
        Assert.IsNull(Thumbnailer.Load(png, -1));
        CollectionAssert.AreEqual(copy, png);
        var tall = Thumbnailer.Load(PngEncoding.Generate(10, 40), 20)!;
        Assert.AreEqual(20, tall.PixelHeight);
        Assert.AreEqual(5, tall.PixelWidth);
        var small = Thumbnailer.Load(PngEncoding.Generate(3, 2))!;
        Assert.AreEqual(3, small.PixelWidth, "never upscale");
    }

    [STATestMethod]
    public void EncodeKeepsPixelSizeAndSetsDpi()
    {
        var png = PngEncoding.Encode(BitmapSource.Create(5, 7, 96, 96, PixelFormats.Bgra32, null, new byte[5 * 7 * 4], 20), dpiScale: 1.5);
        var frame = BitmapDecoder.Create(new MemoryStream(png), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        Assert.AreEqual(5, frame.PixelWidth);
        Assert.AreEqual(7, frame.PixelHeight);
        Assert.AreEqual(144, frame.DpiX, 0.5);
        Assert.IsTrue(CaptureStore.HasPngSignature(png));
    }
}
