namespace Screenshot.Support.Tests;

[TestClass]
public sealed class CapturePipelineTests
{
    private sealed class RecordingCopier(bool fail) : IImageCopier
    {
        public List<byte[]> Copied { get; } = [];
        public void Copy(byte[] image)
        {
            if (fail) throw new InvalidOperationException("Clipboard is busy");
            Copied.Add(image);
        }
    }

    [TestMethod] // CAP-006/008
    public void SavesThenCopies()
    {
        using var t = new TempFolder();
        var copier = new RecordingCopier(fail: false);
        var png = TestImages.Png(4, 3);
        var r = new CapturePipeline(new CaptureStore(t.Path, new FakeDecoder()), copier).Process(png);
        Assert.AreEqual(CaptureResultKind.SavedAndCopied, r.Kind);
        CollectionAssert.AreEqual(png, File.ReadAllBytes(r.SavedPath!));
        CollectionAssert.AreEqual(png, copier.Copied.Single());
    }

    [TestMethod] // T11, CAP-007
    public void CopyFailureKeepsTheFile()
    {
        using var t = new TempFolder();
        var r = new CapturePipeline(new CaptureStore(t.Path, new FakeDecoder()), new RecordingCopier(fail: true)).Process(TestImages.Png(4, 3));
        Assert.AreEqual(CaptureResultKind.SavedNotCopied, r.Kind);
        Assert.IsTrue(File.Exists(r.SavedPath));
        Assert.IsNotNull(r.Error);
    }

    [TestMethod] // CAP-006: nothing is copied when the save fails
    public void SaveFailureCopiesNothing()
    {
        using var t = new TempFolder();
        var copier = new RecordingCopier(fail: false);
        var r = new CapturePipeline(new CaptureStore(t.Path, new FakeDecoder()), copier).Process([0, 1, 2]);
        Assert.AreEqual(CaptureResultKind.NotSaved, r.Kind);
        Assert.IsEmpty(copier.Copied);
    }
}
