# Screenshotz

A small, quiet screenshot tool for **macOS** and **Windows**. Select an area of the screen: it is **saved as a PNG** in `Documents/screenshots` **and copied to your clipboard**, ready to paste.

- One shortcut, two results: a file you keep and an image you can paste right away
- A small preview that never steals your typing focus (Copy · Reveal · Mark Up)
- Recent Screenshots: the newest 20 files, one click away
- Lives in the menu bar (Mac) or system tray (Windows)
- Checks for updates and installs them only when you agree
- Everything stays on your computer

> **Status:** 0.5.0 specification in review. The apps aren't built yet. See [docs/](docs/README.md).

## Shortcuts

| | macOS | Windows |
|---|---|---|
| Capture an area | **⌥S** (Option-S) | **Alt+S** |
| Second shortcut | **⌘⌥4** (Command-Option-4) | **Print Screen** (off by default; turn it on in the menu) |
| Cancel | Esc | Esc or right-click |

Each shortcut can be turned off in the menu. **Capture Area** in the menu always works.

## Install

Downloads are on the [latest release](https://github.com/ashwin-mathias/Screenshots/releases/latest).

### macOS 13 or later
1. Download `Screenshotz-mac.zip`, unzip it, and move **Screenshotz** to **Applications**.
2. Open it. macOS blocks it the first time: go to **System Settings → Privacy & Security** and click **Open Anyway**.
3. The first capture asks for **Screen Recording** permission. Allow it, then reopen Screenshotz if macOS asks.

### Windows 11 (x64)
1. Get **Screenshotz** from the Microsoft Store (link coming once it's live; Store ID `9NM6DV5TJJ6C`).
2. Windows may hide the tray icon: click **^** next to the clock and drag Screenshotz onto the taskbar.

The Store keeps it up to date. Test builds (`Screenshotz-win-x64.msix` on the release page) are for development only. They need the certificate step in [docs/SIGNING.md](docs/SIGNING.md) §3.

On the Mac, updates come from inside the app (**Check for Updates…**). On Windows, the Microsoft Store installs them. Your settings are kept either way.

## Build

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) §7–9. CI builds, tests, signs and publishes both apps when `VERSION` changes.

## Licence

MIT. © 2026 Ashwin Mathias.


[Privacy policy](PRIVACY.md): Screenshotz collects nothing, and your screenshots never leave your computer.
