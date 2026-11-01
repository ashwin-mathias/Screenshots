namespace Screenshot.Support;

/// <summary>Puts an image on the clipboard. Must not touch the clipboard if the image can't be prepared (CLP-002).</summary>
public interface IImageCopier
{
    void Copy(byte[] image);
}

public enum CaptureResultKind
{
    /// <summary>Saved and copied (CAP-008).</summary>
    SavedAndCopied,
    /// <summary>The file is saved, but copying failed (CAP-007).</summary>
    SavedNotCopied,
    /// <summary>Nothing was saved (CAP-006).</summary>
    NotSaved,
}

public sealed record CaptureResult(CaptureResultKind Kind, string? SavedPath, Exception? Error);

/// <summary>File first, then clipboard (CAP-006). A copy failure never removes the saved file (CAP-007).</summary>
public sealed class CapturePipeline(CaptureStore store, IImageCopier copier)
{
    public CaptureResult Process(byte[] png, DateTime? localTime = null)
    {
        string path;
        try
        {
            path = store.Save(png, localTime);
        }
        catch (CaptureStoreException e)
        {
            return new CaptureResult(CaptureResultKind.NotSaved, null, e);
        }

        try
        {
            copier.Copy(png);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return new CaptureResult(CaptureResultKind.SavedNotCopied, path, e);
        }
        return new CaptureResult(CaptureResultKind.SavedAndCopied, path, null);
    }
}
