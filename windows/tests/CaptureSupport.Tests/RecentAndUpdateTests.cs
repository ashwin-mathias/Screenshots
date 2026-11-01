using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace Screenshot.Support.Tests;

[TestClass]
public sealed class RecentCapturesTests
{
    /// <summary>Builds the fixture folder from shared/testcases/recent-order.json.</summary>
    private static TempFolder BuildFixture()
    {
        var t = new TempFolder();
        var links = new List<JsonElement>();
        foreach (var f in SharedCases.Load("recent-order.json").GetProperty("fixtures").EnumerateArray())
        {
            var path = t.File(f.Str("name")!.Replace('/', Path.DirectorySeparatorChar));
            var mtime = DateTime.Parse(f.Str("mtime")!, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            switch (f.Str("kind"))
            {
                case "file":
                    File.WriteAllBytes(path, TestImages.Png(2, 2));
                    File.SetLastWriteTimeUtc(path, mtime);
                    break;
                case "directory":
                    Directory.CreateDirectory(path);
                    Directory.SetLastWriteTimeUtc(path, mtime);
                    break;
                case "hiddenAttribute":
                    if (!OperatingSystem.IsWindows()) break; // only Windows has the attribute
                    File.WriteAllBytes(path, TestImages.Png(2, 2));
                    File.SetLastWriteTimeUtc(path, mtime);
                    File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
                    break;
                case "symlink":
                    links.Add(f); // after the targets exist
                    break;
            }
        }
        foreach (var f in links)
        {
            try { File.CreateSymbolicLink(t.File(f.Str("name")!), t.File(f.Str("target")!)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* no link rights: it can't appear either */ }
        }
        // the top folder's own nested subfolder timestamp is irrelevant; the scan never recurses
        return t;
    }

    [TestMethod] // T12
    public void ScansMatchSharedCases()
    {
        using var t = BuildFixture();
        foreach (var scan in SharedCases.Load("recent-order.json").GetProperty("scans").EnumerateArray())
        {
            var limit = scan.GetProperty("limit").GetInt32();
            var expected = scan.GetProperty("expected").EnumerateArray().Select(e => e.GetString()).ToList();
            var actual = RecentCaptures.Scan(t.Path, limit).Select(r => r.FileName).ToList();
            CollectionAssert.AreEqual(expected, actual, $"limit {limit}: {string.Join(", ", actual)}");
        }
    }

    [TestMethod] // T13
    public void ReadsFreshFromDiskAndReportsMissingFolder()
    {
        using var t = BuildFixture();
        Assert.AreEqual("25.png", RecentCaptures.Scan(t.Path, 1).Single().FileName);
        File.Delete(t.File("25.png"));
        Assert.AreEqual("24.png", RecentCaptures.Scan(t.Path, 1).Single().FileName);
        Assert.ThrowsExactly<RecentCapturesException>(() => RecentCaptures.Scan(t.File("missing")));
    }

    [TestMethod] // ACT-001
    public void RegularFileCheck()
    {
        using var t = new TempFolder();
        File.WriteAllText(t.File("a.png"), "x");
        Directory.CreateDirectory(t.File("d.png"));
        Assert.IsTrue(RecentCaptures.IsRegularFile(t.File("a.png")));
        Assert.IsFalse(RecentCaptures.IsRegularFile(t.File("d.png")));
        Assert.IsFalse(RecentCaptures.IsRegularFile(t.File("gone.png")));
    }
}

[TestClass]
public sealed class UpdateTests
{
    [TestMethod] // T16, shared version-compare.json
    public void VersionsCompareAsSharedCases()
    {
        foreach (var c in SharedCases.Load("version-compare.json").GetProperty("cases").EnumerateArray())
        {
            var tag = AppVersion.ParseTag(c.Str("tag"));
            var current = AppVersion.Parse(c.Str("current"))!.Value;
            var result = tag is not { } v ? "invalid" : v > current ? "newer" : v == current ? "same" : "older";
            Assert.AreEqual(c.Str("result"), result, c.Str("tag"));
        }
    }

    [TestMethod] // T16, shared update-feed.json
    public void FeedMatchesSharedCases()
    {
        var doc = SharedCases.Load("update-feed.json");
        var prefix = doc.Str("prefix")!;
        foreach (var c in doc.GetProperty("cases").EnumerateArray())
        {
            var name = c.Str("name");
            var skipped = c.TryGetProperty("skipped", out var s) && s.ValueKind == JsonValueKind.String ? AppVersion.Parse(s.GetString()) : null;
            var manual = c.TryGetProperty("manual", out var m) && m.ValueKind == JsonValueKind.True;
            var result = UpdateFeed.Evaluate(c.GetProperty("release").GetRawText(), AppVersion.Parse(c.Str("current"))!.Value,
                UpdateFeed.WindowsAsset, skipped, manual, prefix);
            var expected = c.GetProperty("expected");
            if (expected.Str("offer") is { } offer)
            {
                Assert.IsNotNull(result.Offer, $"{name}: {result.Reason}");
                Assert.AreEqual(offer, result.Offer.Version.ToString(), name);
                Assert.AreEqual(expected.Str("notes"), result.Offer.Notes, name);
                Assert.AreEqual($"{prefix}v{offer}/{UpdateFeed.WindowsAsset}", result.Offer.AssetUrl.ToString(), name);
                Assert.AreEqual($"{prefix}v{offer}/{UpdateFeed.WindowsAsset}.sha256", result.Offer.ChecksumUrl.ToString(), name);
            }
            else
            {
                Assert.IsNull(result.Offer, name);
                Assert.AreEqual(expected.Str("ignore"), JsonNamingPolicy.CamelCase.ConvertName(result.Reason.ToString()!), name);
            }
        }
    }

    [TestMethod]
    public void BadJsonIsIgnored()
    {
        Assert.AreEqual(UpdateIgnoreReason.BadResponse, UpdateFeed.Evaluate("<html>", new AppVersion(0, 5, 0), UpdateFeed.WindowsAsset).Reason);
        Assert.AreEqual(UpdateIgnoreReason.BadResponse, UpdateFeed.Evaluate("[]", new AppVersion(0, 5, 0), UpdateFeed.WindowsAsset).Reason);
    }

    [TestMethod] // T17, shared sha256-file.json
    public void ChecksumFilesParseAsSharedCases()
    {
        foreach (var c in SharedCases.Load("sha256-file.json").GetProperty("cases").EnumerateArray())
            Assert.AreEqual(c.Str("expected"), ChecksumFile.Parse(c.Str("text")!), c.Str("text"));
    }

    [TestMethod]
    public void Sha256OfKnownText()
    {
        using var s = new MemoryStream("test"u8.ToArray());
        Assert.AreEqual("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08", ChecksumFile.Sha256Hex(s));
    }
}

[TestClass]
public sealed class MsixInspectorTests
{
    private static X509Certificate2 MakeCert(string subject)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.3")], false));
        var c = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(c.Export(X509ContentType.Pfx), null);
    }

    /// <summary>A minimal package: manifest plus a "PKCX"-prefixed PKCS#7 signature, like a real .msix.</summary>
    private static string MakePackage(TempFolder t, X509Certificate2? signer, string magic = "PKCX")
    {
        var path = t.File("test.msix");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var w = new StreamWriter(zip.CreateEntry("AppxManifest.xml").Open()))
            w.Write("""
                <?xml version="1.0" encoding="utf-8"?>
                <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
                  <Identity Name="AshwinMathias.Screenshot" Publisher="CN=Ashwin Mathias" Version="0.6.0.0" ProcessorArchitecture="x64" />
                </Package>
                """);
        if (signer is not null)
        {
            var cms = new SignedCms(new ContentInfo(Encoding.ASCII.GetBytes("indirect data stand-in")));
            cms.ComputeSignature(new CmsSigner(signer));
            using var s = zip.CreateEntry("AppxSignature.p7x").Open();
            s.Write(Encoding.ASCII.GetBytes(magic));
            s.Write(cms.Encode());
        }
        return path;
    }

    [TestMethod] // T19 (reading side)
    public void ReadsIdentityAndSigner()
    {
        using var t = new TempFolder();
        using var cert = MakeCert("CN=Ashwin Mathias");
        var info = MsixInspector.Inspect(MakePackage(t, cert));
        Assert.AreEqual("AshwinMathias.Screenshot", info.Name);
        Assert.AreEqual("CN=Ashwin Mathias", info.Publisher);
        Assert.AreEqual("0.6.0.0", info.Version);
        Assert.AreEqual(cert.Thumbprint, info.SignerThumbprint);
    }

    [TestMethod] // T19: a different certificate gives a different thumbprint
    public void DifferentCertificateIsDetected()
    {
        using var t = new TempFolder();
        using var ours = MakeCert("CN=Ashwin Mathias");
        using var theirs = MakeCert("CN=Ashwin Mathias");
        Assert.AreNotEqual(ours.Thumbprint, MsixInspector.Inspect(MakePackage(t, theirs)).SignerThumbprint);
    }

    [TestMethod]
    public void UnsignedOrDamagedPackagesFail()
    {
        using (var t = new TempFolder())
            Assert.ThrowsExactly<MsixInspectionException>(() => MsixInspector.Inspect(MakePackage(t, null)));
        using (var t = new TempFolder())
        {
            using var cert = MakeCert("CN=Ashwin Mathias");
            Assert.ThrowsExactly<MsixInspectionException>(() => MsixInspector.Inspect(MakePackage(t, cert, magic: "XXXX")));
        }
        using (var t = new TempFolder())
        {
            File.WriteAllText(t.File("junk.msix"), "not a zip");
            Assert.ThrowsExactly<MsixInspectionException>(() => MsixInspector.Inspect(t.File("junk.msix")));
        }
    }
}
