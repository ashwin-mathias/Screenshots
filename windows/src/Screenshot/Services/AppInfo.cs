using System.Reflection;
using Screenshot.Support;

namespace Screenshot.Services;

/// <summary>Version, build and packaging facts about the running app.</summary>
internal static class AppInfo
{
    private static readonly Assembly Me = typeof(AppInfo).Assembly;

    public static AppVersion Version { get; } =
        AppVersion.Parse(Me.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : null) ?? new AppVersion(0, 0, 0);

    public static string Build { get; } = Metadata("BuildNumber") is { Length: > 0 } b ? b : "0";

    public static string VersionWithBuild => $"{Version} (build {Build})";

    /// <summary>The thumbprint of the certificate updates must be signed with. Empty in local builds.</summary>
    public static string CertThumbprint { get; } = (Metadata("CertThumbprint") ?? "").Trim().ToUpperInvariant();

    /// <summary>True when running from the installed MSIX (needed for updates and launch at login).</summary>
    public static bool IsPackaged { get; } = DetectPackage();

    public static string? PackageFamilyName { get; private set; }

    private static bool DetectPackage()
    {
        try
        {
            PackageFamilyName = global::Windows.ApplicationModel.Package.Current.Id.FamilyName;
            return true;
        }
        catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    private static string? Metadata(string key) =>
        Me.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value;
}
