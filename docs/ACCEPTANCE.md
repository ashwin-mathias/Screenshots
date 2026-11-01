# Screenshotz — Acceptance tests

A release is "done" only when the automated tests pass in CI **and** every manual check below passes on a real **Mac** and a real **Windows 11 PC**. Section R lists bugs that were shipped and fixed; each one is a regression test.

Record at the top of each run: version and build, OS version, architecture, display setup (scales), and install path. Tick each box per platform: **[M]** macOS, **[W]** Windows. A check with only one tag applies only to that platform.

Safety while testing:
- Use generated images and a test folder where possible.
- Manual paste tests overwrite the clipboard, so use throwaway content.
- Never put confidential screen content in test records.

---

## T. Automated (CI, both platforms unless marked)

| ID | Covers | Pass condition |
|---|---|---|
| T01 | STO-001/002 | Saving into a missing nested folder creates it; saved bytes equal the input PNG |
| T02 | STO-003 | Two saves at the same moment with default ids give different names |
| T03 | STO-004 | Same date and id twice: the second save fails, the first file is byte-identical |
| T04 | STO-002 | Invalid bytes: save fails, no file or folder is created for it |
| T05 | STO-002 | A decodable non-PNG (JPEG) is rejected by save |
| T06 | STO-002 | A zero-size or malformed image fails without a file |
| T07 | STO-001/004 | A regular file where the folder should be: save fails, that file is unchanged |
| T08 | STO-003 | `file-naming.json`: every case matches; path-like ids can't leave the folder |
| T09 | CLP-001 | Copy to a private or fake clipboard: PNG bytes equal the source; TIFF [M] / DIBV5 [W] decodes to the same size; no text or file path is published |
| T10 | CLP-002 | Invalid image: copy fails and the clipboard is unchanged (change count [M] / fake write log [W]) |
| T11 | CAP-007 | A failing clipboard after save: the file exists, the result says "saved, not copied" |
| T12 | REC-002 | `recent-order.json`: 25 PNGs plus a hidden file, JPG, `dir.png` folder, link and nested PNG → exactly the newest 20 in order; ties A→Z; `.PNG` included; limit 0 and −1 → empty |
| T13 | REC-001 | Delete the newest file: a fresh limit-1 scan returns the next one; a missing folder → error |
| T14 | REC-003 | `display-names.json`: UUID stripped only when it's the trailing suffix |
| T15 | PRE-004 | Thumbnail of a 12 × 8 image at max 6 is 6 × 4; invalid image → none; max 0 → none; source bytes unchanged |
| T16 | UPD-002 | `version-compare.json` and `update-feed.json`: offered version, asset links and every "ignore" reason (missing checksum, wrong host, bad tag, pre-release) |
| T17 | UPD-004 | `sha256-file.json`; a checksum mismatch deletes the download and reports the safety-check error |
| T18 | UPD-004 [M] | A test app signed with a different certificate fails the certificate check |
| T19 | UPD-004 [W] | A test package signed with a different certificate fails the thumbprint check |
| T20 | PRE-005 | With a test clock: dismiss at 8 s; hover cancels; leaving restarts a full 8 s |
| K01–K06 [M] | KEY-001/002/003 | Carbon harness (local only, keys must be free): mappings, both register, each fires once, disable/re-enable, a taken key names itself and the other still works, release-all |
| K07–K10 [W] | KEY-001/002/003 | Hotkey harness (local only): Alt+S registers; a key taken by a helper process reports its name and the other still works; Print Screen is not registered while the Windows setting is on; release-all |

## A. Install and update

- [ ] A1 [M] Fresh install from `Screenshotz-mac.zip`: "Open Anyway" is needed once. The app opens with one menu-bar icon and the Welcome window.
- [ ] A0 [W] Store build: **Get** from the Microsoft Store installs with no certificate step; Start menu shows **Screenshotz**; the menu has no Check for Updates Automatically item, and **Check for Updates…** opens the Store page. With a network monitor, the app makes no network requests (UPD-007).
- [ ] A2 [W] Test build on a fresh PC: installing the `.msix` without the certificate fails with a clear Windows message. After importing `Screenshotz-signing.cer` into Local Machine → Trusted People it installs. Start menu shows "Screenshotz"; tray icon and Welcome appear.
- [ ] A3 Check for Updates… on the latest version says "You're up to date…".
- [ ] A4 With a newer test release: the prompt appears with notes; **Later** closes it and leaves "Install Update X…" in the menu; nothing is downloaded (check the temp folder and network log).
- [ ] A5 **Install and Relaunch**: progress shows in the status line; the app quits, updates and reopens; About shows the new version; "Updated to Screenshotz X" once.
- [ ] A6 After the update, settings and shortcuts are unchanged. [M] Screen Recording permission is still granted (no new prompt).
- [ ] A7 **Skip This Version**: automatic checks don't offer it again; a manual check still does.
- [ ] A8 Test release without the `.sha256` file: not offered. Wrong checksum: "didn't pass its safety check", nothing installed, old version still runs.
- [ ] A9 Turn off **Check for Updates Automatically** and restart: no network traffic from the app (check with Little Snitch / Resource Monitor).
- [ ] A10 Start an update while a capture is in progress: the update waits until the capture finishes.
- [ ] A11 [M] App in a read-only folder: the "can't update itself in this folder" message with Open Download Page.

## B. Capture

- [ ] B1 From another app, press shortcut 1 (⌥S / Alt+S), let go, drag: one PNG in the folder and the same image pastes into a second app (Notes / Word / Teams).
- [ ] B2 Shortcut 2 (⌘⌥4 / Print Screen, after turning it on): the same result, with no second selection.
- [ ] B3 Both shortcuts off: menu **Capture Area** still captures.
- [ ] B4 Esc during selection: no new file; the clipboard still holds the earlier throwaway content.
- [ ] B5 [W] Right-click and a tiny click (no drag) also cancel.
- [ ] B6 Press the shortcut repeatedly during selection and while saving: only one selection, one file.
- [ ] B7 The saved PNG has the right signature and the **physical** pixel size (Retina / 150 % / 200 %). The pasted image has the same size.
- [ ] B8 [W] Mixed scales (for example a 100 % external + 150 % laptop): capture on each display; the selected area matches what was dragged, exactly.
- [ ] B9 [W] The overlay covers only the display with the pointer; the selection stops at its edges.
- [ ] B10 [W] After capture or cancel, typing continues in the app that had focus before.
- [ ] B11 Blocked folder (replace `screenshots` with a file in a test account): clear error, no false success; restore and capture again.
- [ ] B12 Simulated clipboard failure (test build switch): file kept, warning shown, retry thumbnail, no checkmark; Copy in the thumbnail then works.
- [ ] B13 Quit during selection: selection stops, shortcuts are released, relaunch works.
- [ ] B14 [M] Deny Screen Recording: capture doesn't start; Open System Settings / Later work; after approval (and reopen if asked) capture works.
- [ ] B15 [M] Revoke permission while running: next capture explains how to fix it instead of claiming success.
- [ ] B16 [M] Hold Control during selection: note the clipboard-only result and check How to Use explains it.
- [ ] B17 [W] Capture a window with a menu or tooltip open: it appears in the image (frozen frame).

## C. Shortcuts and login

- [ ] C1 Turn each shortcut off and on separately, restart: choices kept and actually delivered.
- [ ] C2 [M] With ⌥S off, ß types again. [W] With Alt+S off, Outlook's Alt+S sends again.
- [ ] C3 Take one shortcut with another app first: startup names the conflict; the other shortcut and menu capture still work.
- [ ] C4 [W] Turn on Print Screen while Windows' Print Screen setting is on: the explanation and Open Settings appear, item stays unchecked. Turn the Windows setting off, try again: it works.
- [ ] C5 Launch at Login on → sign out and in: the app starts once. Off → it doesn't.
- [ ] C6 [M] "Approval Needed…" opens Login Items. [W] Disabled in Task Manager → "Turn On in Settings…" opens Startup apps.
- [ ] C7 Launch the app twice: one icon, the Welcome window comes forward.

## D. Recents and actions

- [ ] D1 25 PNGs and excluded items in a test folder: menu shows the newest 20 in order; the menu stays responsive.
- [ ] D2 Long names: readable labels, UUID removed only at the end, full name and date in the tooltip.
- [ ] D3 Edit an old file, rename one, add one, delete one; reopen the menu: it matches the disk by modification time.
- [ ] D4 Delete a file after the menu opened: Copy / Open / Reveal / Mark Up each show their own error; the app keeps working.
- [ ] D5 Open uses the default image app; Reveal selects the exact file; Copy pastes that exact image.
- [ ] D6 Mark Up opens the exact file in Preview [M] / Paint [W].
- [ ] D7 Restart: Reveal Last is disabled, Show Last Screenshot Preview works; after a capture Reveal Last works.
- [ ] D8 Empty folder: "No screenshots yet" + Open Screenshots Folder. Unreadable folder: "Could not read screenshots folder".

## E. Preview

- [ ] E1 Capture while typing in another app: the thumbnail appears and **typing continues in that app** without clicking.
- [ ] E2 With the pointer away, it closes after about 8 s; hovering keeps it; leaving gives a fresh 8 s.
- [ ] E3 Copy, Reveal and Mark Up in the preview each act on the exact file and close the preview.
- [ ] E4 Thumbnails off: no preview after capture; Show Last Screenshot Preview still works; the choice survives a restart.
- [ ] E5 Start a capture while the preview, Welcome and Help are open: all hide first and don't appear in the image.
- [ ] E6 Very wide, very tall, huge (8000 × 4000) and corrupt files: aspect fit or "preview unavailable"; originals unchanged.
- [ ] E7 On a second display, and over a full-screen app / another Space or desktop: the preview appears on the capture display, inside its usable area, and doesn't steal focus.

## F. Windows, text and look

- [ ] F1 Welcome shows the active shortcuts; [W] the tray-pinning tip.
- [ ] F2 About: version and build, author row with avatar, Sparky card with photo and note, privacy line, Check for Updates…, licence link. No QR or payment codes.
- [ ] F3 Release History lists `changelog.json`, newest first, with LATEST on the first.
- [ ] F4 Light and dark mode, 100 % and 200 % scale, minimum Help size: nothing clipped.
- [ ] F5 VoiceOver [M] / Narrator [W] read the status icon, preview image, close button and buttons.

## G. Build and privacy

- [ ] G1 CI passes on both jobs; the release has the five assets; checksums match.
- [ ] G2 [M] `codesign --verify --strict`, `plutil -lint` pass; the app is universal (`lipo -archs` shows arm64 x86_64).
- [ ] G3a [W] Partner Center accepts `Screenshotz-store-x64.msixupload` (package validation passes) and certification passes.
- [ ] G3 [W] `signtool verify /pa` passes on the test build; the manifest identity and version are right.
- [ ] G4 Source review: no network code except the updater; no analytics; no keyboard hook / Input Monitoring.

## R. Regression tests

(None yet. Add one row for every bug fixed: `R1 (0.5.x) — what broke — how to check it.`)
