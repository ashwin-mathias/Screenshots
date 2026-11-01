# Builds dist/Screenshot-win-x64.msix (ARCHITECTURE §7).
#   pwsh windows/scripts/build-msix.ps1                       # unsigned, for local testing
#   pwsh windows/scripts/build-msix.ps1 -PfxPath c.pfx -PfxPassword *** -Thumbprint ABC…   # signed (CI)
param(
    [string]$BuildNumber = "0",
    [string]$PfxPath = "",
    [string]$PfxPassword = "",
    [string]$Thumbprint = ""
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$windows = Split-Path -Parent $PSScriptRoot
$repo = Split-Path -Parent $windows
$version = (Get-Content (Join-Path $repo 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "VERSION must be X.Y.Z, found '$version'." }
$out = Join-Path $windows 'out'
$publish = Join-Path $out 'publish'
$dist = Join-Path $windows 'dist'
$msix = Join-Path $dist 'Screenshot-win-x64.msix'
Remove-Item $out, $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $publish, $dist | Out-Null

$signed = $PfxPath -ne ''
if ($signed -and $Thumbprint -eq '') { throw 'A signed build needs -Thumbprint (the updater pins it).' }
if (-not $signed) { Write-Output '::warning::Building an UNSIGNED package. It installs only with Developer Mode and can never install updates.' }

# 1. Publish the app, self-contained, with the certificate pin built in.
dotnet publish (Join-Path $windows 'src/Screenshot/Screenshot.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:BuildNumber=$BuildNumber -p:WindowsCertThumbprint=$Thumbprint -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# 2. Manifest and logos.
$manifest = Get-Content (Join-Path $windows 'packaging/AppxManifest.xml') -Raw
Set-Content -Path (Join-Path $publish 'AppxManifest.xml') -Value $manifest.Replace('__VERSION__', "$version.0") -Encoding utf8
$images = Join-Path $publish 'Images'
New-Item -ItemType Directory -Force $images | Out-Null
$icons = Join-Path $repo 'shared/assets/icon/windows'
foreach ($name in 'Square44x44Logo', 'Square150x150Logo', 'Wide310x150Logo', 'StoreLogo') {
    Copy-Item (Join-Path $icons "$name.scale-200.png") (Join-Path $images "$name.png")
}

# 3. Pack.
$kit = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64" -Directory | Sort-Object Name | Select-Object -Last 1
if (-not $kit) { throw 'Windows SDK tools (makeappx, signtool) not found.' }
$makeappx = Join-Path $kit.FullName 'makeappx.exe'
$signtool = Join-Path $kit.FullName 'signtool.exe'
& $makeappx pack /o /h SHA256 /d $publish /p $msix
if ($LASTEXITCODE -ne 0) { throw 'makeappx pack failed.' }

# 4. Sign (retry the timestamp server a few times) and verify.
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
    $cer = Join-Path $dist 'Screenshot-signing.cer'
    [IO.File]::WriteAllBytes($cer, $cert.Export('Cert'))
    if ($env:CI) {
        # Trust it on the throwaway runner so signtool can verify the whole chain.
        Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
        Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
        & $signtool verify /pa $msix
        if ($LASTEXITCODE -ne 0) { throw 'signtool verify failed.' }
    }
}

# 5. Checksum file, in sha256sum format.
$hash = (Get-FileHash $msix -Algorithm SHA256).Hash.ToLower()
Set-Content -Path "$msix.sha256" -Value "$hash  Screenshot-win-x64.msix" -NoNewline -Encoding ascii
Write-Output "Built $msix ($version, build $BuildNumber, $(if ($signed) { 'signed' } else { 'unsigned' }))"
Write-Output "SHA-256 $hash"
