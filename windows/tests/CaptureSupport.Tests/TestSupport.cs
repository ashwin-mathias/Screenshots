using System.IO.Compression;
using System.Text.Json;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Screenshot.Support.Tests;

/// <summary>Loads shared/testcases/*.json from the repo.</summary>
internal static class SharedCases
{
    private static readonly Lazy<string> Folder = new(() =>
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, "shared", "testcases");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("shared/testcases not found above " + AppContext.BaseDirectory);
    });

    public static JsonElement Load(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder.Value, file)));
        return doc.RootElement.Clone();
    }

    public static string? Str(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

/// <summary>A fresh temp folder per test, deleted afterwards.</summary>
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "screenshot-tests-" + Guid.NewGuid().ToString("N"));
    public TempFolder() => Directory.CreateDirectory(Path);
    public string File(string name) => System.IO.Path.Combine(Path, name);
    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

/// <summary>Makes real PNG files and decodes their size from the IHDR chunk (enough for the store's rules).</summary>
internal static class TestImages
{
    public static byte[] Png(int width, int height)
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        WriteBE(ihdr, 0, width); WriteBE(ihdr, 4, height);
        ihdr[8] = 8; ihdr[9] = 6; // 8-bit RGBA
        Chunk(ms, "IHDR", ihdr);
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (var x = 0; x < width; x++) raw.Write([(byte)(x * 20), (byte)(y * 30), 128, 255]);
        }
        using var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true)) raw.WriteTo(zs);
        Chunk(ms, "IDAT", z.ToArray());
        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    /// <summary>Starts like a JPEG; the fake decoder reports it as a 4×4 image.</summary>
    public static byte[] FakeJpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; WriteBE(len, 0, data.Length); s.Write(len);
        var td = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(td, 0);
        data.CopyTo(td, 4);
        s.Write(td);
        var crc = new byte[4]; WriteBE(crc, 0, (int)System.IO.Hashing.Crc32Compat.Compute(td)); s.Write(crc);
    }

    private static void WriteBE(byte[] b, int at, int v)
    {
        b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
    }
}

internal sealed class FakeDecoder : IImageDecoder
{
    public PixelSize? TryDecodeSize(byte[] data)
    {
        if (CaptureStore.HasPngSignature(data) && data.Length >= 24)
        {
            int Read(int at) => (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];
            return new PixelSize(Read(16), Read(20));
        }
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return new PixelSize(4, 4);
        return null;
    }
}
