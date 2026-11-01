using System.Globalization;
using System.Text.RegularExpressions;

namespace Screenshot.Support.Tests;

[TestClass]
public sealed class FileNamingTests
{
    [TestMethod] // T08, shared file-naming.json
    public void NamesMatchSharedCases()
    {
        foreach (var c in SharedCases.Load("file-naming.json").GetProperty("names").EnumerateArray())
        {
            var local = DateTime.ParseExact(c.Str("local")!, "yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture);
            Assert.AreEqual(c.Str("expected"), FileNaming.FileName(local, c.Str("id")), c.Str("id"));
        }
    }

    [TestMethod]
    public void SanitizeMatchesSharedCases()
    {
        foreach (var c in SharedCases.Load("file-naming.json").GetProperty("sanitize").EnumerateArray())
            Assert.AreEqual(c.Str("expected"), FileNaming.Sanitize(c.Str("input")!), c.Str("input"));
    }

    [TestMethod]
    public void EmptyIdentifierFallsBackToUuid()
    {
        var name = FileNaming.FileName(new DateTime(2026, 11, 1, 8, 0, 0), "///");
        StringAssert.Matches(name, new Regex("^Screenshot 2026-11-01 at 08\\.00\\.00\\.000-[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{12}\\.png$"));
    }

    [TestMethod] // T14, shared display-names.json
    public void DisplayNamesMatchSharedCases()
    {
        foreach (var c in SharedCases.Load("display-names.json").GetProperty("cases").EnumerateArray())
            Assert.AreEqual(c.Str("expected"), FileNaming.DisplayName(c.Str("file")!), c.Str("file"));
    }

    [TestMethod] // shared png-validation.json
    public void PngSignatureMatchesSharedCases()
    {
        foreach (var c in SharedCases.Load("png-validation.json").GetProperty("cases").EnumerateArray())
            Assert.AreEqual(c.GetProperty("png").GetBoolean(), CaptureStore.HasPngSignature(Convert.FromHexString(c.Str("hex")!)), c.Str("hex"));
    }
}

[TestClass]
public sealed class CaptureStoreTests
{
    private static readonly DateTime When = new(2026, 11, 1, 14, 3, 27, 125);

    [TestMethod] // T01
    public void SavesExactBytesIntoMissingNestedFolder()
    {
        using var t = new TempFolder();
        var folder = Path.Combine(t.Path, "Documents", "screenshots");
        var png = TestImages.Png(12, 8);
        var path = new CaptureStore(folder, new FakeDecoder()).Save(png, When);
        Assert.IsTrue(Directory.Exists(folder));
        CollectionAssert.AreEqual(png, File.ReadAllBytes(path));
        Assert.AreEqual(folder, Path.GetDirectoryName(path));
    }

    [TestMethod] // T02
    public void TwoSavesAtSameMomentGetDifferentNames()
    {
        using var t = new TempFolder();
        var store = new CaptureStore(t.Path, new FakeDecoder());
        var a = store.Save(TestImages.Png(2, 2), When);
        var b = store.Save(TestImages.Png(2, 2), When);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod] // T03
    public void NeverOverwrites()
    {
        using var t = new TempFolder();
        var store = new CaptureStore(t.Path, new FakeDecoder());
        var first = TestImages.Png(3, 3);
        var path = store.Save(first, When, "SAME");
        var e = Assert.ThrowsExactly<CaptureStoreException>(() => store.Save(TestImages.Png(5, 5), When, "SAME"));
        Assert.AreEqual(CaptureStoreError.FileExists, e.Error);
        CollectionAssert.AreEqual(first, File.ReadAllBytes(path));
    }

    [TestMethod] // T04
    public void InvalidBytesCreateNothing()
    {
        using var t = new TempFolder();
        var folder = Path.Combine(t.Path, "never-made");
        var e = Assert.ThrowsExactly<CaptureStoreException>(() => new CaptureStore(folder, new FakeDecoder()).Save([1, 2, 3], When));
        Assert.AreEqual(CaptureStoreError.InvalidImage, e.Error);
        Assert.IsFalse(Directory.Exists(folder));
    }

    [TestMethod] // T05
    public void DecodableNonPngIsRejected()
    {
        using var t = new TempFolder();
        var e = Assert.ThrowsExactly<CaptureStoreException>(() => new CaptureStore(t.Path, new FakeDecoder()).Save(TestImages.FakeJpeg(), When));
        Assert.AreEqual(CaptureStoreError.NotPng, e.Error);
        Assert.IsEmpty(Directory.GetFileSystemEntries(t.Path));
    }

    [TestMethod] // T06
    public void ZeroSizeImageIsRejected()
    {
        using var t = new TempFolder();
        var e = Assert.ThrowsExactly<CaptureStoreException>(() => new CaptureStore(t.Path, new FakeDecoder()).Save(TestImages.Png(0, 5), When));
        Assert.AreEqual(CaptureStoreError.InvalidImage, e.Error);
        Assert.IsEmpty(Directory.GetFileSystemEntries(t.Path));
    }

    [TestMethod] // T07
    public void FileInPlaceOfFolderFailsAndIsUntouched()
    {
        using var t = new TempFolder();
        var blocker = t.File("screenshots");
        File.WriteAllText(blocker, "keep me");
        var e = Assert.ThrowsExactly<CaptureStoreException>(() => new CaptureStore(blocker, new FakeDecoder()).Save(TestImages.Png(2, 2), When));
        Assert.AreEqual(CaptureStoreError.FolderUnavailable, e.Error);
        Assert.AreEqual("keep me", File.ReadAllText(blocker));
    }
}
