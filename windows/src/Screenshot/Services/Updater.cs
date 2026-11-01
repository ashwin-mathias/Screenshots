using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Screenshot.Interop;
using Screenshot.Support;
using AddPackageOptions = global::Windows.Management.Deployment.AddPackageOptions;
using PackageManager = global::Windows.Management.Deployment.PackageManager;

namespace Screenshot.Services;

/// <summary>
/// Checks GitHub for a newer release and installs it only after the user agrees (SPEC §13).
/// Network use is limited to the release check and an approved download (UPD-006).
/// </summary>
internal sealed class Updater : IDisposable
{
    public static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private readonly Settings _settings;
    private readonly HttpClient _http;
    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private CancellationTokenSource? _download;

    /// <summary>An update was found that the user should be asked about.</summary>
    public event Action<UpdateOffer, bool>? OfferFound;

    public UpdateOffer? Pending { get; private set; }
    public bool IsDownloading => _download is not null;

    public Updater(Settings settings)
    {
        _settings = settings;
        _http = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,        // GitHub redirects downloads; https → http is never followed
            MaxAutomaticRedirections = 5,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        })
        { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Screenshotz", AppInfo.Version.ToString()));
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(Windows)"));

        _timer = new System.Windows.Threading.DispatcherTimer { Interval = FirstCheckDelay };
        _timer.Tick += async (_, _) =>
        {
            _timer.Interval = TimeSpan.FromHours(1); // re-evaluate hourly; checks only when 24 h have passed
            await AutomaticCheckAsync();
        };
    }

    /// <summary>Starts the automatic schedule (UPD-001).</summary>
    public void Start() => _timer.Start();

    private async Task AutomaticCheckAsync()
    {
        if (_settings.AutoUpdateCheckDisabled || IsDownloading) return;
        if (_settings.LastUpdateCheck is { } last && DateTime.UtcNow - last < CheckInterval) return;
        try
        {
            await CheckAsync(manual: false);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            Log.Write($"Automatic update check failed: {e.Message}"); // silent (UPD-001)
        }
    }

    /// <summary>Checks now. Throws on network errors so a manual check can explain them.</summary>
    public async Task<UpdateCheckResult> CheckAsync(bool manual)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UpdateFeed.LatestReleaseApi);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await _http.SendAsync(request);
        _settings.LastUpdateCheck = DateTime.UtcNow;
        _settings.Save();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"GitHub answered {(int)response.StatusCode} {response.ReasonPhrase}.");

        var json = await response.Content.ReadAsStringAsync();
        var skipped = AppVersion.Parse(_settings.SkippedVersion);
        var result = UpdateFeed.Evaluate(json, AppInfo.Version, UpdateFeed.WindowsAsset, skipped, manual);
        Log.Write($"Update check ({(manual ? "manual" : "automatic")}): {(result.Offer is { } o ? "offer " + o.Version : result.Reason.ToString())}");
        if (result.Offer is { } offer)
        {
            Pending = offer;
            OfferFound?.Invoke(offer, manual);
        }
        else if (result.Reason == UpdateIgnoreReason.UpToDate)
        {
            Pending = null;
        }
        return result;
    }

    public void Skip(UpdateOffer offer)
    {
        _settings.SkippedVersion = offer.Version.ToString();
        _settings.Save();
        Pending = null;
    }

    public void CancelDownload() => _download?.Cancel();

    /// <summary>
    /// Downloads, checks (UPD-004) and installs (UPD-005). Windows closes and restarts the app on success,
    /// so this normally doesn't return; any failure throws <see cref="UpdateException"/>.
    /// </summary>
    public async Task InstallAsync(UpdateOffer offer, IProgress<int> progress)
    {
        if (!AppInfo.IsPackaged) throw new UpdateException(Strings.UpdatesNeedInstalledApp, offerDownloadPage: true);
        if (AppInfo.CertThumbprint.Length == 0) throw new UpdateException("This build isn't signed for release, so it can't install updates.", offerDownloadPage: true);

        _download = new CancellationTokenSource();
        var folder = Path.Combine(Path.GetTempPath(), "Screenshot-update-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(folder, UpdateFeed.WindowsAsset);
        try
        {
            Directory.CreateDirectory(folder);
            var expected = ChecksumFile.Parse(await GetStringAsync(offer.ChecksumUrl, _download.Token))
                ?? throw new UpdateException($"{Strings.UpdateUnsafe} The checksum file is malformed.");
            await DownloadAsync(offer.AssetUrl, file, progress, _download.Token);

            string actual;
            await using (var s = File.OpenRead(file)) actual = ChecksumFile.Sha256Hex(s);
            if (actual != expected) throw new UpdateException($"{Strings.UpdateUnsafe} The download doesn't match its checksum.");

            MsixInfo info;
            try { info = MsixInspector.Inspect(file); }
            catch (MsixInspectionException e) { throw new UpdateException($"{Strings.UpdateUnsafe} {e.Message}"); }
            if (info.SignerThumbprint != AppInfo.CertThumbprint)
                throw new UpdateException($"{Strings.UpdateUnsafe} It isn't signed with Screenshotz's certificate.");
            if (info.Name != "AshwinMathias.ScreenshotzTest" || info.Publisher != "CN=Ashwin Mathias")
                throw new UpdateException($"{Strings.UpdateUnsafe} It's a different app.");
            if (info.Version != $"{offer.Version}.0")
                throw new UpdateException($"{Strings.UpdateUnsafe} It contains version {info.Version}, not {offer.Version}.");

            _settings.PendingUpdateVersion = offer.Version.ToString();
            _settings.Save();
            Native.RegisterApplicationRestart("--updated", 0);
            Log.Write($"Installing {offer.Version}");
            var result = await new PackageManager().AddPackageByUriAsync(new Uri(file), new AddPackageOptions { ForceAppShutdown = true });
            if (result.ExtendedErrorCode is { } err)
                throw new UpdateException($"Windows couldn't install the update. {result.ErrorText} (0x{err.HResult:X8})", offerDownloadPage: true);
        }
        catch (OperationCanceledException) when (_download.IsCancellationRequested)
        {
            _settings.PendingUpdateVersion = null;
            _settings.Save();
            throw new UpdateException("The update was cancelled.", cancelled: true);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException
                                      or OperationCanceledException or System.Runtime.InteropServices.COMException)
        {
            _settings.PendingUpdateVersion = null;
            _settings.Save();
            throw new UpdateException($"The update couldn't be downloaded or installed. {e.Message}", offerDownloadPage: true);
        }
        catch (UpdateException)
        {
            _settings.PendingUpdateVersion = null;
            _settings.Save();
            throw;
        }
        finally
        {
            _download.Dispose();
            _download = null;
            try { Directory.Delete(folder, recursive: true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    private async Task<string> GetStringAsync(Uri url, CancellationToken ct)
    {
        using var r = await _http.GetAsync(url, ct);
        RequireHttps(r);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadAsStringAsync(ct);
    }

    private async Task DownloadAsync(Uri url, string file, IProgress<int> progress, CancellationToken ct)
    {
        using var download = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 })
        { Timeout = Timeout.InfiniteTimeSpan };
        download.DefaultRequestHeaders.UserAgent.ParseAdd($"Screenshotz/{AppInfo.Version} (Windows)");
        using var r = await download.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        RequireHttps(r);
        r.EnsureSuccessStatusCode();
        var total = r.Content.Headers.ContentLength ?? -1;
        await using var source = await r.Content.ReadAsStreamAsync(ct);
        await using var target = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long done = 0;
        var last = -1;
        int n;
        while ((n = await source.ReadAsync(buffer, ct)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, n), ct);
            done += n;
            var pct = total > 0 ? (int)(done * 100 / total) : 0;
            if (pct != last) { progress.Report(pct); last = pct; }
        }
    }

    /// <summary>Redirects may only end at https (UPD-002).</summary>
    private static void RequireHttps(HttpResponseMessage r)
    {
        if (r.RequestMessage?.RequestUri is { } u && u.Scheme != Uri.UriSchemeHttps)
            throw new UpdateException($"{Strings.UpdateUnsafe} The download wasn't served over https.");
    }

    public void Dispose()
    {
        _timer.Stop();
        _download?.Cancel();
        _http.Dispose();
    }
}

internal sealed class UpdateException(string message, bool offerDownloadPage = false, bool cancelled = false) : Exception(message)
{
    public bool OfferDownloadPage { get; } = offerDownloadPage;
    public bool Cancelled { get; } = cancelled;
}
