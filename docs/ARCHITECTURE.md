# Screenshotz — Architecture

How the two apps are built: the stack, the code layout, the shared files, signing and the release pipeline.

---

## 1. Approach

- **Two native apps and one spec.**
  - Swift/AppKit on macOS.
  - C#/WPF on Windows.
- The logic shared between them is small (saving, naming, recents, version checks). It is written twice and kept identical by **shared test cases** (§5), not by shared code.
- Each app has a **support library** (no UI, fully testable) and an **app** that holds the UI and system wiring.
- **No third-party runtime libraries** on either platform. Build-time and test-only packages are limited to the Microsoft ones listed in §3.

## 2. Repo layout

```
VERSION                     0.5.0 — the only place the version is written
CLAUDE.md                   rules for agents working in this repo
README.md  LICENSE  .gitignore
docs/                       SPEC, DESIGN, ARCHITECTURE, DECISIONS, ACCEPTANCE, REBUILD_PROMPT, make-bundle.sh
shared/
  about.json                author name and role, Sparky note
  changelog.json            release history, newest first (bundled in both apps)
  assets/                   avatar.png, sparky.jpg, icon/ (generated, committed)
  testcases/                JSON cases both test suites run (§5)
macos/
  Package.swift
  Sources/CaptureSupport/   CaptureStore, FileNaming, RecentCaptures, Thumbnailer, Version, UpdateFeed
  Sources/Screenshot/       AppDelegate, MenuController, CaptureController, GlobalShortcut,
                            PermissionController, LoginItemController, PreviewController,
                            WelcomeWindow, HelpAboutWindow, Updater, UpdateInstaller
  Tests/CaptureSupportTests/
  Tests/ShortcutTests/      (needs free keys; skipped on CI)
  Resources/Info.plist
  scripts/build-app.sh, test.sh, preview-check.sh
windows/
  Screenshot.sln
  src/CaptureSupport/       (net10.0, runs anywhere) FileNaming, CaptureStore, CapturePipeline,
                            RecentCaptures, AppVersion, UpdateFeed, ChecksumFile, MsixInspector
  src/CaptureSupport.Windows/ (WPF imaging) WicImageDecoder, Thumbnailer, PngEncoding,
                            ClipboardPayload + DibV5Format, ImageCopier, Win32Clipboard
  src/Screenshot/           App, AppController (menu, shortcuts, capture flow, update prompts)
    Interop/                Native (P/Invoke), MessageHost, TrayIcon, HotkeyManager, Screens, Theme
    Services/               Settings + Log, Strings, AppInfo, LoginItem, FileActions, Updater
    Windows/                SelectionOverlay, PreviewWindow, WelcomeWindow, HelpAboutWindow, AlertWindow
  tests/CaptureSupport.Tests/          (net10.0: shared cases, store, recents, updates, pipeline)
  tests/CaptureSupport.Windows.Tests/  (Windows only: WIC, clipboard payload, thumbnails)
  packaging/AppxManifest.xml
  scripts/build-msix.ps1
tools/make-icons.py         generates shared/assets/icon/*
tools/make-certs.sh         creates the signing certificates and secrets (SIGNING.md)
.github/workflows/build.yml
.github/workflows/check-signing.yml   one-off check that both certificates sign
```

## 3. Stack

| | macOS | Windows |
|---|---|---|
| Language | Swift 5.9+ (language mode 5) | C# 13 |
| Toolchain | Xcode 16 / SwiftPM (`swift-tools-version: 5.9`) | .NET 10 SDK (LTS) |
| UI | AppKit | WPF with the Fluent theme (`ThemeMode="System"`); the tray icon uses `Shell_NotifyIcon` directly (no WinForms) |
| Target | macOS 13.0, universal (`arm64` + `x86_64`) | `net10.0-windows10.0.22621.0`, `win-x64`, self-contained |
| System APIs | AppKit, Carbon (hotkeys), CoreGraphics (permission), ImageIO, ServiceManagement, Security (signatures), Foundation | Win32 via hand-written P/Invoke (`Interop/Native.cs`), WinRT via the Windows SDK projection (`PackageManager`, `StartupTask`), WIC / `System.Windows.Media.Imaging`. One Microsoft package at run time: `System.Security.Cryptography.Pkcs` (reads the update's signature) |
| External tools | `/usr/sbin/screencapture`, `/usr/bin/ditto`, `/usr/bin/codesign`, Preview.app | Paint (via the PNG "edit" verb), File Explorer |
| Tests | XCTest (`swift test`) | MSTest (`dotnet test`); the platform-neutral tests also run on macOS and Linux |

## 4. Components

| Responsibility | macOS | Windows |
|---|---|---|
| Entry, single instance, lifecycle | `AppDelegate` | `App` (mutex) + `MessageHost` (hidden top-level window; finds and signals the running copy) |
| Menu and status | `MenuController` (NSStatusItem + NSMenu) | `TrayIcon` (Shell_NotifyIcon) + `AppController` (WPF ContextMenu) |
| Capture state machine | `CaptureController` (runs `screencapture`) | `AppController.CaptureAsync` (`Screens.Grab` + `SelectionOverlay` + `CapturePipeline`) |
| Shortcuts | `GlobalShortcut` (Carbon) | `HotkeyManager` (`RegisterHotKey` on the hidden host window) |
| Permission | `PermissionController` | — |
| Launch at login | `LoginItemController` (SMAppService) | `LoginItem` (StartupTask) |
| Save, validate, name | `CaptureStore`, `FileNaming` | `CaptureStore`, `FileNaming` |
| Clipboard | `CaptureStore.copy(to: NSPasteboard)` | `ImageCopier` → `ClipboardPayload` → `Win32Clipboard` (behind `IClipboardSink` so tests use a fake) |
| Recents | `RecentCaptures` | `RecentCaptures` |
| Thumbnail decode | `Thumbnailer` (ImageIO) | `Thumbnailer` (WIC) |
| Preview | `PreviewController` (NSPanel) | `PreviewWindow` |
| Help / Welcome | `HelpAboutWindow`, `WelcomeWindow` | same names |
| Update check | `UpdateFeed` (parse, compare, validate URLs) + `Updater` (timer, prompt, download) | same |
| Update install | `UpdateInstaller` (verify, helper script) | `Updater.InstallAsync` (checksum, `MsixInspector` signer pin, PackageManager) |

**Test seams:** the support libraries take the date, UUID, folder, clipboard, HTTP client and file system from outside, so tests can control them. CAP-007 and the update failure paths are tested by passing in a failing clipboard or HTTP client.

## 5. Shared files

- **`shared/changelog.json`:**
  ```json
  [ { "version": "0.5.0", "date": "2026-11-01", "title": "…", "notes": ["…", "…"] } ]
  ```
  - Newest first.
  - Bundled into both apps (macOS `Contents/Resources/changelog.json`; Windows next to the exe).
  - CI uses the matching entry as the GitHub release notes.
- **`shared/about.json`:**
  ```json
  { "name": "Ashwin Mathias", "role": "Creator of Screenshotz", "dog": "Sparky", "sparkyNote": "…" }
  ```
- **`shared/testcases/`:** JSON files that **both** test suites load and run. A behaviour rule lives in one place, and a difference between the apps shows up as a failing test.

  | File | What it pins down |
  |---|---|
  | `file-naming.json` | date + id → expected file name; unsafe ids → cleaned names |
  | `recent-order.json` | file list with dates, flags and links → expected order and limit results |
  | `display-names.json` | file name → menu label (UUID stripping) |
  | `png-validation.json` | byte prefixes and sizes → accept or reject |
  | `version-compare.json` | pairs of tags → newer / same / older / invalid |
  | `update-feed.json` | sample GitHub release JSON → offered version, asset links or the reason it's ignored |
  | `sha256-file.json` | checksum file contents → valid hash or error |

## 6. Threads

- UI changes run on the main (UI) thread only.
- Off the UI thread:
  - folder reads and the newest-file lookup;
  - update checks and downloads;
  - signature checks;
  - on macOS, waiting for `screencapture`;
  - on Windows, grabbing the display and encoding the PNG.
- Saving and clipboard writes may run on the main thread. They are small and must stay ordered (file first). Docs must not claim that all file work is asynchronous.

## 7. Packaging

### macOS
- **`bash macos/scripts/build-app.sh`:**
  1. `swift build -c release --arch arm64 --arch x86_64`.
  2. Assemble `dist/Screenshotz.app`:
     - `Contents/MacOS/Screenshotz`;
     - `Info.plist`;
     - `Resources/AppIcon.icns`, `changelog.json`, `about.json`, `avatar.png`, `sparky.jpg`.
  3. Sign with the certificate named in `MACOS_SIGN_IDENTITY`. Without it, sign ad-hoc for local builds and print a warning.
  4. Run `codesign --verify --strict` and `plutil -lint`.
  5. Zip with `ditto -c -k --keepParent` to `dist/Screenshotz-mac.zip`.
- **Info.plist:**
  - `CFBundleName` and `CFBundleDisplayName` "Screenshotz";
  - `CFBundleIdentifier` `local.ashwinmathias.Screenshot`;
  - `CFBundleShortVersionString` from `VERSION`;
  - `CFBundleVersion` = CI run number;
  - `CFBundlePackageType` APPL, `CFBundleIconFile` AppIcon;
  - `LSMinimumSystemVersion` 13.0, `LSUIElement` true, `NSHighResolutionCapable` true, `NSPrincipalClass` NSApplication;
  - `NSHumanReadableCopyright` "© 2026 Ashwin Mathias · MIT licence".
  - Usage descriptions:
    - Screen capture: "Capture the area you select, save it to Documents/screenshots, and copy it to your clipboard."
    - Documents folder: "Save your screenshots in Documents/screenshots."

### Windows
- **Two Windows builds from the same code**, chosen by `-Channel` (fixed at build time as the `Channel` assembly metadata, read by `AppInfo.IsStoreBuild`):

  | | Store build (`-Channel store`) | Test build (`-Channel test`, default) |
  |---|---|---|
  | Identity | `AshwinMathias.Screenshotz`, `CN=7E8438D5-12B0-4FC0-9098-FE994BBCE149` | `AshwinMathias.Screenshot`, `CN=Ashwin Mathias` |
  | Signed by | Microsoft, after upload (left unsigned) | our self-signed certificate |
  | Output | `dist/Screenshotz-store-x64.msixupload` (a zip that holds the `.msix`) | `dist/Screenshotz-win-x64.msix` + `.sha256` + `Screenshotz-signing.cer` |
  | Updates | Microsoft Store (UPD-007); updater code never runs | in-app updater from GitHub releases |

- **`pwsh windows/scripts/build-msix.ps1`:**
  1. `dotnet publish src/Screenshot -c Release -r win-x64 --self-contained -p:PublishSingleFile=false`.
  2. Copy `AppxManifest.xml`, with the version filled in, plus the scale-200 logos as `Images/<name>.png` (no resource index, so no scale or theme variants yet).
  3. `makeappx pack /d <folder> /p dist/Screenshotz-win-x64.msix`.
  4. `signtool sign /fd SHA256 /f <pfx> /p <password> /tr http://timestamp.digicert.com /td SHA256`.
     - Without the certificate, the package is left unsigned for local testing, with a warning.
  5. `signtool verify /pa` (CI trusts the public certificate on the throwaway runner first), then write `<msix>.sha256` and `Screenshotz-signing.cer`.
- **AppxManifest.xml:**
  - `Identity Name="AshwinMathias.Screenshot" Publisher="CN=Ashwin Mathias" Version="X.Y.Z.0" ProcessorArchitecture="x64"`;
  - `DisplayName` "Screenshotz", `PublisherDisplayName` "Ashwin Mathias";
  - `TargetDeviceFamily Windows.Desktop MinVersion="10.0.22000.0"`;
  - capability `runFullTrust`;
  - `uap:VisualElements` with the logos (Square44x44, Square150x150, Wide310x150, StoreLogo);
  - extension `desktop:StartupTask TaskId="ScreenshotStartup" Enabled="false" DisplayName="Screenshotz"`.
- **App manifest (`app.manifest`):** `PerMonitorV2` DPI awareness; `asInvoker`.
- **Certificate pin:** the build passes the Windows certificate thumbprint (`WINDOWS_CERT_THUMBPRINT`) as `-p:WindowsCertThumbprint`, stored as assembly metadata and read by UPD-004. Local unsigned builds get an empty pin, so they never accept updates.
- The MSIX version is `X.Y.Z.0`. The build number appears only in the About text, read from an assembly attribute.

## 8. Signing, CI and releases

### Certificates (created once by Ashwin; step-by-step in [SIGNING.md](SIGNING.md))
- **Recommended:** `tools/make-certs.sh` in a Codespace creates both certificates with `openssl`.
  - It exports them as `.p12` / `.pfx` in the classic **PBE-SHA1-3DES / SHA-1 MAC** format, because some macOS versions can't import OpenSSL 3's default AES format.
  - It checks its own output, saves the five secrets with `gh secret set`, and leaves a git-ignored `.signing-backup/` to download.
- **Proof:** `.github/workflows/check-signing.yml` (run by hand) imports and signs a test program on a macOS and a Windows runner.
- The manual Keychain Access / PowerShell route below is the fallback.
- **macOS:**
  - A self-signed **Code Signing** certificate named "Screenshot Signing (Ashwin Mathias)", made in Keychain Access → Certificate Assistant → Create a Certificate (type: Code Signing), valid 10 years.
  - Export it as `.p12`.
  - Secrets: `MACOS_CERT_P12_BASE64`, `MACOS_CERT_PASSWORD`.
- **Windows:**
  - `New-SelfSignedCertificate` with subject `CN=Ashwin Mathias`, the code-signing EKU, RSA 3072, valid 10 years (exact commands in SIGNING.md).
  - Export the `.pfx` (private) and `.cer` (public). CI exports the public `.cer` again from the `.pfx` for each release.
  - Secrets: `WINDOWS_CERT_PFX_BASE64`, `WINDOWS_CERT_PASSWORD`, `WINDOWS_CERT_THUMBPRINT`.
- **Keep an offline backup of both private files.**
  - A lost or replaced Windows certificate means a new publisher, so every PC must uninstall and reinstall.
  - A lost Mac certificate means macOS asks for Screen Recording permission again, and updates fail the certificate check until the user installs the new build by hand.
- Never commit `.p12`, `.pfx` or `.cer` private material. `.gitignore` blocks them, and CI fails if any are tracked.

### Workflow `.github/workflows/build.yml`
- **Triggers:**
  - push to `main` (ignoring `docs/**` and `**.md`);
  - pull requests;
  - manual run.
- **Job `mac` (`macos-15`):**
  1. `swift test` in `macos/`.
  2. Import the certificate into a temporary keychain (pushes to `main` only).
  3. `build-app.sh`, then compute SHA-256.
  4. Upload the artifact.
- **Job `windows` (`windows-latest`):**
  1. `dotnet test`.
  2. Decode the PFX to `$RUNNER_TEMP` (pushes to `main` only).
  3. `build-msix.ps1` twice: the signed test build and the Store build.
  4. Upload both as artifacts.
- **Job `release` (`ubuntu-latest`):**
  - Needs both jobs. Runs only on `main`, and only if tag `v$(cat VERSION)` doesn't exist yet.
  - Checks that `changelog.json`'s first entry matches `VERSION`, or fails with `::error::`.
  - Creates the GitHub release `vX.Y.Z` with the assets from SPEC §14. The body is that changelog entry.
- Compiler and test errors are turned into `::error::` annotations, so they are visible without opening the log.
- Missing secrets on `main` fail the build with an `::error::` that names the secret.
- **Pinning:** actions are pinned to commit SHAs, with the version in a comment.

## 9. Release checklist

1. Change the code and update the docs it touches (see CLAUDE.md).
2. Bump `VERSION`. Add the changelog entry. If the shortcuts or install steps changed, update README.
3. Push to `main`. Watch both CI jobs. The release is created only when every job passes.
4. **Windows:** in Partner Center, start a new submission, upload `Screenshotz-store-x64.msixupload` from the release, update "What's new" from the changelog, and submit (STORE.md). It reaches PCs through the Store after certification, usually 1–3 days.
5. **macOS:** install the update through **Check for Updates…** (this tests the updater too). Run the ACCEPTANCE items marked for that release on both machines.
