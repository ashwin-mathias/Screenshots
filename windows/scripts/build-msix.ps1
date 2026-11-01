# Builds a Windows package (ARCHITECTURE §7).
#   -Channel test (default): dist/test/Screenshotz-win-x64.msix, self-signed when -PfxPath is given (CI), else unsigned
#   -Channel store:          dist/store/Screenshotz-store-x64.msixupload, unsigned (Microsoft signs it), Store identity
param(
    [ValidateSet('test', 'store')] [string]$Channel = 'test',
    [string]$BuildNumber = "0",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [string]$Thumbprint = ""
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Package identities (SPEC §3). The Store values come from Partner Center → Product identity (docs/STORE.md).
$identity = @{
    test  = @{ Name = 'AshwinMathias.Screenshot';  Publisher = 'CN=Ashwin Mathias' }
    store = @{ Name = 'AshwinMathias.Screenshotz'; Publisher = 'CN=7E8438D5-12B0-4FC0-9098-FE994BBCE149' }
}[$Channel]

$windows = Split-Path -Parent $PSScriptRoot
$repo = Split-Path -Parent $windows
$version = (Get-Content (Join-Path $repo 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "VERSION must be X.Y.Z, found '$version'." }
$out = Join-Path $windows "out/$Channel"
$publish = Join-Path $out 'publish'
$dist = Join-Path $windows "dist/$Channel"
$msixName = if ($Channel -eq 'store') { 'Screenshotz-store-x64.msix' } else { 'Screenshotz-win-x64.msix' }
$msix = Join-Path $dist $msixName
Remove-Item $out, $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $publish, $dist | Out-Null

$signed = $PfxPath -ne ''
if ($Channel -eq 'store' -and $signed) { throw 'Store packages are uploaded unsigned; Microsoft signs them.' }
if ($signed -and $Thumbprint -eq '') { throw 'A signed build needs -Thumbprint (the updater pins it).' }
if ($Channel -eq 'test' -and -not $signed) { Write-Output '::warning::Building an UNSIGNED test package. It installs only with Developer Mode.' }

# 1. Publish the app, self-contained. The channel and certificate pin are fixed at build time.
dotnet publish (Join-Path $windows 'src/Screenshot/Screenshot.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:BuildNumber=$BuildNumber -p:Channel=$Channel -p:WindowsCertThumbprint=$Thumbprint -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# 2. Manifest and logos.
$manifest = (Get-Content (Join-Path $windows 'packaging/AppxManifest.xml') -Raw).
    Replace('__VERSION__', "$version.0").Replace('__NAME__', $identity.Name).Replace('__PUBLISHER__', $identity.Publisher)
Set-Content -Path (Join-Path $publish 'AppxManifest.xml') -Value $manifest -Encoding utf8
$images = Join-Path $publish 'Images'
New-Item -ItemType Directory -Force $images | Out-Null
$icons = Join-Path $repo 'shared/assets/icon/windows'
foreach ($name in 'Square44x44Logo', 'Square150x150Logo', 'Wide310x150Logo', 'StoreLogo') {
    Copy-Item (Join-Path $icons "$name.scale-200.png") (Join-Path $images "$name.png")
}

$kit = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64" -Directory | Sort-Object Name | Select-Object -Last 1
if (-not $kit) { throw 'Windows SDK tools (makepri, makeappx, signtool) not found.' }
$makepri = Join-Path $kit.FullName 'makepri.exe'
$makeappx = Join-Path $kit.FullName 'makeappx.exe'
$signtool = Join-Path $kit.FullName 'signtool.exe'

# 3. Resource index (resources.pri), as Visual Studio makes it, so Windows doesn't index the package
#    itself during install. The config file stays outside the package (a stray priconfig.xml breaks installs).
$priConfig = Join-Path $out 'priconfig.xml'
& $makepri createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o
if ($LASTEXITCODE -ne 0) { throw 'makepri createconfig failed.' }
& $makepri new /pr $publish /cf $priConfig /mn (Join-Path $publish 'AppxManifest.xml') /of (Join-Path $publish 'resources.pri') /o
if ($LASTEXITCODE -ne 0) { throw 'makepri new failed.' }
Get-ChildItem $publish -Filter 'priconfig*.xml' | Remove-Item

# 4. Pack.
& $makeappx pack /o /h SHA256 /d $publish /p $msix
if ($LASTEXITCODE -ne 0) { throw 'makeappx pack failed.' }

if ($Channel -eq 'store') {
    # 5s. The upload file Partner Center expects: a zip holding the .msix.
    $upload = Join-Path $dist 'Screenshotz-store-x64.msixupload'
    $zip = "$upload.zip"
    Compress-Archive -Path $msix -DestinationPath $zip -CompressionLevel Optimal
    Move-Item $zip $upload
    Remove-Item $msix
    Write-Output "Built $upload ($version, build $BuildNumber, Store identity $($identity.Name))"
    return
}

# 5. Sign (retry the timestamp server a few times) and verify.
if ($signed) {
    $ok = $false
    foreach ($attempt in 1..3) {
        & $signtool sign /fd SHA256 /f $PfxPath /p $PfxPassword /tr 'http://timestamp.digicert.com' /td SHA256 $msix
        if ($LASTEXITCODE -eq 0) { $ok = $true; break }
        Start-Sleep -Seconds (5 * $attempt)
    }
    if (-not $ok) { throw 'signtool sign failed.' }

    $cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new($PfxPath, $PfxPassword)
    if ($cert.Thumbprint -ne $Thumbprint.Trim().ToUpper()) { throw 'The .pfx does not match WINDOWS_CERT_THUMBPRINT.' }
    $cer = Join-Path $dist 'Screenshotz-signing.cer'
    [IO.File]::WriteAllBytes($cer, $cert.Export('Cert'))
    if ($env:CI) {
        # Trust it on the throwaway runner so signtool can verify the whole chain.
        Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
        Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
        & $signtool verify /pa $msix
        if ($LASTEXITCODE -ne 0) { throw 'signtool verify failed.' }
    }
}

# 6. Checksum file, in sha256sum format.
$hash = (Get-FileHash $msix -Algorithm SHA256).Hash.ToLower()
Set-Content -Path "$msix.sha256" -Value "$hash  $msixName" -NoNewline -Encoding ascii
Write-Output "Built $msix ($version, build $BuildNumber, $(if ($signed) { 'signed' } else { 'unsigned' }))"
Write-Output "SHA-256 $hash"
