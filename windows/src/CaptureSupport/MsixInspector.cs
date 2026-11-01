using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Xml.Linq;

namespace Screenshot.Support;

/// <summary>What an .msix package says about itself.</summary>
public sealed record MsixInfo(string Name, string Publisher, string Version, string SignerThumbprint, string SignerSubject);

public sealed class MsixInspectionException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Reads the identity and the signing certificate of an .msix (UPD-004).
/// Windows itself checks that the package contents match the signature when it installs;
/// this check makes sure the signer is our own certificate before we ask Windows to install it.
/// </summary>
public static class MsixInspector
{
    private static readonly byte[] P7xMagic = "PKCX"u8.ToArray();

    public static MsixInfo Inspect(string msixPath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(msixPath);
            var (name, publisher, version) = ReadIdentity(zip);
            var signer = ReadSigner(zip);
            return new MsixInfo(name, publisher, version, signer.Thumbprint.ToUpperInvariant(), signer.Subject);
        }
        catch (MsixInspectionException) { throw; }
        catch (Exception e) when (e is InvalidDataException or IOException or CryptographicException or System.Xml.XmlException or UnauthorizedAccessException)
        {
            throw new MsixInspectionException($"The update package couldn't be read. {e.Message}", e);
        }
    }

    private static (string Name, string Publisher, string Version) ReadIdentity(ZipArchive zip)
    {
        var entry = zip.GetEntry("AppxManifest.xml") ?? throw new MsixInspectionException("The update package has no manifest.");
        using var s = entry.Open();
        var doc = XDocument.Load(s);
        var identity = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Identity")
            ?? throw new MsixInspectionException("The update package manifest has no identity.");
        string Attr(string n) => identity.Attribute(n)?.Value ?? throw new MsixInspectionException($"The update package identity has no {n}.");
        return (Attr("Name"), Attr("Publisher"), Attr("Version"));
    }

    private static System.Security.Cryptography.X509Certificates.X509Certificate2 ReadSigner(ZipArchive zip)
    {
        var entry = zip.GetEntry("AppxSignature.p7x") ?? throw new MsixInspectionException("The update package isn't signed.");
        byte[] bytes;
        using (var s = entry.Open())
        using (var ms = new MemoryStream())
        {
            s.CopyTo(ms);
            bytes = ms.ToArray();
        }
        if (bytes.Length <= P7xMagic.Length || !bytes.AsSpan(0, P7xMagic.Length).SequenceEqual(P7xMagic))
            throw new MsixInspectionException("The update package signature is in an unknown format.");

        var cms = new SignedCms();
        cms.Decode(bytes.AsSpan(P7xMagic.Length));
        if (cms.SignerInfos.Count != 1) throw new MsixInspectionException("The update package must have exactly one signer.");
        var signer = cms.SignerInfos[0];
        // Checks the signature itself (not the trust chain, which Windows checks on install).
        signer.CheckSignature(verifySignatureOnly: true);
        return signer.Certificate ?? throw new MsixInspectionException("The update package signature has no certificate.");
    }
}
