namespace Screenshot.Support;

/// <summary>Width and height of a decoded image, in pixels.</summary>
public readonly record struct PixelSize(int Width, int Height);

/// <summary>Decodes images. Production uses WIC; tests can use a simple fake.</summary>
public interface IImageDecoder
{
    /// <summary>The pixel size if <paramref name="data"/> decodes as an image, otherwise null.</summary>
    PixelSize? TryDecodeSize(byte[] data);
}

public enum CaptureStoreError
{
    /// <summary>The bytes don't decode to an image with a positive size.</summary>
    InvalidImage,
    /// <summary>The image decodes but isn't a PNG.</summary>
    NotPng,
    /// <summary>The folder can't be created or used.</summary>
    FolderUnavailable,
    /// <summary>A file with that exact name already exists (never overwritten).</summary>
    FileExists,
    /// <summary>Writing the file failed.</summary>
    WriteFailed,
}

public sealed class CaptureStoreException(CaptureStoreError error, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public CaptureStoreError Error { get; } = error;
}

/// <summary>Saves the exact PNG bytes into the screenshots folder (STO-001 to STO-004).</summary>
public sealed class CaptureStore(string folder, IImageDecoder decoder, Func<DateTime>? localNow = null)
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public string Folder { get; } = folder;

    public static bool HasPngSignature(ReadOnlySpan<byte> data) =>
        data.Length >= PngSignature.Length && data[..PngSignature.Length].SequenceEqual(PngSignature);

    /// <summary>Creates the folder and any missing parents. Never picks another folder.</summary>
    public void EnsureFolder()
    {
        try
        {
            Directory.CreateDirectory(Folder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new CaptureStoreException(CaptureStoreError.FolderUnavailable,
                $"Couldn't create the screenshots folder at {Folder}. {e.Message}", e);
        }
        if (!Directory.Exists(Folder))
            throw new CaptureStoreException(CaptureStoreError.FolderUnavailable,
                $"Couldn't create the screenshots folder at {Folder}.");
    }

    /// <summary>
    /// Checks the image, then writes it with create-new semantics. Returns the saved path.
    /// The image is checked before the folder is touched, so bad input creates nothing.
    /// </summary>
    public string Save(byte[] png, DateTime? localTime = null, string? identifier = null)
    {
        if (decoder.TryDecodeSize(png) is not { Width: > 0, Height: > 0 })
            throw new CaptureStoreException(CaptureStoreError.InvalidImage, "The captured image is empty or damaged.");
        if (!HasPngSignature(png))
            throw new CaptureStoreException(CaptureStoreError.NotPng, "The captured image isn't a PNG.");

        EnsureFolder();
        var name = FileNaming.FileName(localTime ?? localNow?.Invoke() ?? DateTime.Now, identifier);
        var path = Path.Combine(Folder, name);

        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException e) when (File.Exists(path))
        {
            throw new CaptureStoreException(CaptureStoreError.FileExists,
                $"A screenshot named \"{name}\" already exists, so nothing was overwritten.", e);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new CaptureStoreException(CaptureStoreError.WriteFailed,
                $"Couldn't save the screenshot in {Folder}. {e.Message}", e);
        }

        try
        {
            using (stream)
            {
                stream.Write(png);
                stream.Flush(flushToDisk: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The file is ours and incomplete; remove it so no broken screenshot is left behind.
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw new CaptureStoreException(CaptureStoreError.WriteFailed,
                $"Couldn't save the screenshot in {Folder}. {e.Message}", e);
        }
        return path;
    }
}
