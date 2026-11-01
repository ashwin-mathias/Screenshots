# Screenshotz — Decisions and lessons

Why the app is the way it is. Read this before changing behaviour, so settled questions aren't reopened by accident. Newest first.

---

## Product decisions (as agreed with Ashwin)

| Version | Decision | Reason |
|---|---|---|
| 0.5 | **Native app on each platform**: Swift/AppKit for macOS, C#/.NET 10 + WPF for Windows. One spec and shared test cases keep them in step | The shared logic is small; native apps stay small, fast and feel right on each OS. A cross-platform framework or shared core costs more than it saves |
| 0.5 | **Targets: macOS 13+ (universal) and Windows 11 x64** | Ashwin's machines. Windows 10 is out of support. ARM64 Windows later if needed. Universal Mac costs nothing extra |
| 0.5 | **Windows shortcuts: Alt+S (on) and Print Screen (optional, off)** | Two keys, close together, same letter as ⌥S on the Mac. Win+S is reserved by Windows (Search); Win-key shortcuts can't be registered. Ctrl+letter shortcuts break common commands. Alt+S costs Outlook's Send shortcut (Ctrl+Enter still sends); pressing Alt then S for the Office ribbon still works |
| 0.5 | **One display per capture** on both platforms | Matches macOS; avoids mixed-DPI stitching on Windows |
| 0.5 | **Windows draws its own selection overlay on a frozen frame** | Windows has no command-line area capture. The Snipping Tool link gives clipboard only and no file or cancel signal, which would break "file first". A frozen frame also captures menus and tooltips as they were |
| 0.5 | **Updates: check GitHub, ask first, verify, install** (check at launch and daily; can be turned off; Install / Later / Skip). Nothing downloads without consent | Ashwin wants updates in the app, with the user always agreeing first. Replaces the 0.4 "no network" rule with "network only for updates" |
| 0.5 | **Update safety = SHA-256 checksum + same signing certificate** as the running app; links only from this repo's releases | Same idea as couch-lab 2.5.2. The checksum catches corruption; the certificate check is the real protection |
| 0.5 | **Repo is public** (MIT) | Lets the app read releases without a token, and gives free CI minutes. Mac CI minutes cost 10× on private repos. Nothing secret is in the code; certificates live in Actions secrets |
| 0.5 | **Self-signed certificates** for both platforms, kept in Actions secrets with an offline backup | Free. A stable certificate keeps macOS's Screen Recording permission across updates (0.4's ad-hoc signing lost it on every rebuild) and lets MSIX updates install over each other. A paid Apple or Windows certificate is only needed to remove first-install warnings for other people |
| 0.5 | **Certificates are made by `tools/make-certs.sh` in a Codespace** and proven by the "Check signing" workflow; the manual Mac/PC steps are the fallback | One script, no clicking on two machines, secrets set automatically. Keys made in GitHub's cloud is acceptable because they're stored in GitHub secrets anyway. Exported in the classic 3DES format so macOS can import them |
| 0.5 | **Windows installs as MSIX** | Clean install and uninstall, built-in startup task, signed updates through `PackageManager` |
| 0.5 | **Releases only when `VERSION` changes**; tags `vX.Y.Z`; one version for both apps | Every push builds and tests, but users only see real versions. One number avoids "Mac 0.5 vs Windows 0.5.3" confusion |
| 0.5 | **Version restarts at 0.5.0** | Ashwin's choice; follows the 0.4 macOS app |
| 0.5 | **The app is named Screenshotz everywhere** (Store, Start menu, Mac app, menus, windows, release files). Internal IDs stay: Mac bundle ID `local.ashwinmathias.Screenshot` (settings and Screen Recording permission carry over from 0.4), the repo name, and the certificate names. Saved files keep "Screenshot …" because there it describes the file | "Screenshot" was taken in the Microsoft Store. Ashwin chose one name everywhere over a Store-only name |
| 0.5 | **Welcome can be minimised on Windows too** (minimise button, no maximise) | Ashwin missed it on first run; matches the Mac |
| 0.5 | **Windows test builds use the package name `AshwinMathias.ScreenshotzTest`** | The first failed install of `AshwinMathias.Screenshot` left records on Ashwin's PC that block that name (R2). A fresh name avoids risky edits to Windows' app database and matches the new app name |
| 0.5 | **Windows ships through the Microsoft Store** (free for individuals since 2025); the self-signed `.msix` stays for test builds | Removes the certificate step for everyone else; Microsoft signs Store builds |
| 0.5 | **Branding like couch-lab**: personal project; About has the author row (avatar, Ashwin Mathias, "Creator of Screenshotz") and a **Sparky card** (photo + note). **No payment or QR codes** | A screenshot tool may be shared more widely than a TV app; keep payment handles out |
| 0.5 | **Sparky card text:** "Designed with love for Sparky, who always camps out where I work." | Ashwin's wording |
| 0.5 | **New app icon**, drawn from code (`tools/make-icons.py`): blue-to-teal tile, four white capture corners, centre crosshair; matching monochrome tray glyphs and a checkmark variant | Ashwin chose a new icon over recovering the 0.4 files. Generated from code, so every size stays consistent and easy to change |
| 0.5 | Removed from the 0.4 spec: "Company Genentech", "Developed by Codex…" credit, the work-laptop path, and historical verification records | Personal app; the new docs describe the product, not one past build |
| 0.5 | Release history moves from `CHANGELOG.md` to `shared/changelog.json` | One structured file for both apps and for the GitHub release notes; no Markdown parser needed |
| 0.5 | Tests use real frameworks (XCTest, MSTest) plus **shared JSON test cases** | 0.4's tests were standalone executables. Shared cases stop the two apps drifting apart |
| 0.4 | Save the file **before** copying; a copy failure keeps the file and offers retry | A file you have beats an all-or-nothing promise |
| 0.4 | Two independent shortcuts; Option-S chosen despite losing ß | Fast, one-handed; typing trade-off disclosed |
| 0.4 | Preview never takes focus; 8 s timer that restarts after hover | Paste into the app you were typing in, without clicking back |
| 0.4 | Recents read from disk each time, newest 20 by modification date | No database to go stale; includes edited and copied-in files |

## Still to confirm

(None open.)

## Check on real devices (assumptions to verify in 0.5 testing)

- **Print Screen registration:**
  - assumed: when `PrintScreenKeyForSnippingEnabled = 1`, Windows takes the key first, so the app must not register it;
  - check that Print Screen works once that setting is off.
- **Restart after an MSIX update:** assumed `RegisterApplicationRestart` brings the app back after `ForceApplicationShutdown`. If not, the app must ask the user to reopen it from Start.
- **HDR displays on Windows:** GDI captures of HDR screens can look washed out. If this is a problem, switch `ScreenGrabber` to Desktop Duplication (DXGI) with tone mapping.
- **Self-signed certificate on macOS 13–15:** confirm that Screen Recording permission really survives an update signed with the same self-signed certificate.

## Engineering lessons (don't repeat these)

1. **Ad-hoc signing makes macOS forget permissions.** Each build gets a new identity. Sign every build with the same certificate.
2. **Never call a shell with user or file data.** Use argument arrays (`screencapture`, `ditto`, the update helper).
3. **Check before you clear.** Prepare every clipboard format before emptying the clipboard.
4. **Don't trust "latest" blindly.** Check the tag format, the asset link prefix, the checksum and the certificate before installing anything.
5. **Never commit signing keys or passwords**, even to a private repo. Deleting a file doesn't remove it from git history; a leaked key must be replaced.
6. **CI errors are invisible without annotations.** Keep the `::error::` steps.
7. **Hard-coded Documents paths break with OneDrive.** Always ask Windows for the Documents known folder.
8. **OpenSSL 3's default `.p12` encryption (AES) can't be imported on some macOS versions.** Export with `-keypbe PBE-SHA1-3DES -certpbe PBE-SHA1-3DES -macalg sha1`, and check the result.
9. **A message-only window (HWND_MESSAGE) never receives broadcasts** such as `WM_SETTINGCHANGE` or `TaskbarCreated`. The Windows host window is a hidden top-level window, so theme changes and Explorer restarts reach it.
10. **An MSIX needs a real `resources.pri` and no stray language folders.** Without one, Windows indexes the package at install time; .NET's satellite folders (`de/`, `ja/` …) or a stray `priconfig.xml` then fail the install with 0x80070490. Build the index with `makepri` (config file outside the package) and set `SatelliteResourceLanguages=en`.
11. **A failed MSIX install can leave records that block that package name for good** (0x80070490, "Failure in the Indexed state handler", before any file is read). Test with a fresh package name before changing the package; don't edit Windows' app database by hand.
