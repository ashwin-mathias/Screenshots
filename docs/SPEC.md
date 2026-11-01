# Screenshotz — Product specification

Version described: **0.5.0** (November 2026). Status: **draft for approval**.

This file says **what the app does** on both platforms. How it looks is in [DESIGN.md](DESIGN.md), how it's built in [ARCHITECTURE.md](ARCHITECTURE.md), why it is this way in [DECISIONS.md](DECISIONS.md), and how to prove it works in [ACCEPTANCE.md](ACCEPTANCE.md).

Words used:
- **must** = required; **should** = strong default; **may** = allowed, not required.
- Each requirement has an ID (for example `CAP-006`). The ID is the same on both platforms.
- A requirement is **Core** unless marked otherwise. Core means both apps behave the same way.
- **macOS:** and **Windows:** lines say how a Core requirement is met on that platform. They never change the observable behaviour, unless the line says "platform difference".
- Quoted on-screen text must be used exactly, including `·`, `…` and `—`.

---

## 1. Purpose and users

- **A small, quiet utility that captures an area of the screen and puts it in two places at once:**
  - a **PNG file** in the user's `Documents/screenshots` folder, and
  - an **image on the clipboard**, ready to paste.
- Used by one person (Ashwin) on his own **Mac** and **Windows PC**, many times a day, to paste screen regions into documents, chats and slides while keeping a file copy.
- The app lives in the **menu bar (macOS)** or **system tray (Windows)**. It has no main window, no account and no server.
- App name: **Screenshotz**. Repo: `ashwin-mathias/Screenshots` (public, MIT licence).

## 2. Principles

1. **Two outputs, one action.** Every successful capture means a file on disk *and* image data on the clipboard. A message alone is not success.
2. **Never lose or damage a screenshot.** No overwrites, no automatic deletion, no changes to the original file.
3. **Don't interrupt typing.** The preview never takes keyboard focus.
4. **Honest status.** Every outcome — success, cancel, partial success, failure — is reported as what actually happened.
5. **Private and local.** Screenshots never leave the machine. The only network traffic is the update check and update downloads the user approves (§13).
6. **Same app on both platforms.** Same features, same words, same rules. Only platform conventions (shortcuts, menu location, system dialogs) differ.
7. **Plain language.** Messages are short and say what to do next.

## 3. Platforms and identity

| | macOS | Windows |
|---|---|---|
| Minimum OS | macOS 13 Ventura | Windows 11 (any supported release) |
| Architectures | Universal: Apple silicon + Intel | x64 only (ARM64 later) |
| Where the app lives | Menu bar (no Dock icon) | System tray (no taskbar button) |
| App ID | Bundle ID `local.ashwinmathias.Screenshot` | **Store:** `AshwinMathias.Screenshotz`, publisher `CN=7E8438D5-12B0-4FC0-9098-FE994BBCE149`, family name `AshwinMathias.Screenshotz_gg9r6ndhn3xw6`, Store ID `9NM6DV5TJJ6C`. **Test builds:** `AshwinMathias.Screenshot`, publisher `CN=Ashwin Mathias` |
| Delivered as | `Screenshotz-mac.zip` containing `Screenshotz.app` | **Microsoft Store** (the "Store build"). Test builds: `Screenshotz-win-x64.msix` |
| Signed with | Self-signed code-signing certificate (§14) | Store build: signed by Microsoft. Test builds: self-signed certificate (§14) |
| Updates | In-app updater (§13) | Store build: the Microsoft Store (§13, UPD-007). Test builds: the in-app updater, from `Screenshotz-win-x64.msix` on GitHub releases |
| Default shortcuts | **⌥S** (Option-S) and **⌘⌥4** (Command-Option-4), both on | **Alt+S** on; **Print Screen** off (optional) |
| Screenshot folder | `~/Documents/screenshots` | `Documents\screenshots` (the Windows *Documents* known folder) |
| Mark Up opens in | Preview | The PNG "Edit" app (Paint by default) |

- Version: **0.5.0** for both apps, from the `VERSION` file at the repo root. The build number is the CI run number.
- The two apps always ship **the same version together**.

### Scope boundaries (not in 0.5)
- No cloud upload, account, telemetry, OCR, video, scrolling capture, window or full-screen capture modes, timer, destination picker, format picker, custom shortcut editor or built-in annotation editor.
- Mark Up hands the file to the system's image editor.
- Recent Screenshots is a view of the folder, not a library or database.
- Adding any of these needs a decision recorded in DECISIONS.md first.

---

## 4. User journeys

### 4.1 First capture
1. The user opens Screenshotz. The app icon appears in the menu bar / tray. On first launch the **Welcome** window opens (§11.2).
2. The user presses a shortcut or chooses **Capture Area**.
3. **macOS only:** if Screen Recording permission is missing, the app asks for it and explains how to turn it on (§8). The user tries again after approving.
4. The user lets go of the shortcut keys, drags a rectangle and releases the mouse.
5. The app **saves the PNG first, then copies it**. On full success the status reads "Saved and copied to clipboard" and the icon briefly shows a checkmark.
6. If thumbnails are on, a small preview appears in the corner. The user can paste straight away.

### 4.2 Cancel, retry and partial success
- **Esc** during selection: no file, and the app doesn't touch the clipboard.
- If the copy fails after the file is saved, the file is kept and the preview offers **Copy** to try again.
- If the file can't be saved, the app never claims success.

### 4.3 Find an older screenshot
- **Recent Screenshots** (menu or Welcome window) lists the newest 20 PNG files in the folder, read fresh from disk.
- Each one has **Open**, **Copy Image**, **Reveal**, **Show Preview** and **Mark Up**.
- This works for screenshots from earlier sessions and for any other PNG the user put in the folder.

### 4.4 After a restart
- Shortcut, thumbnail and update choices are kept.
- Welcome does not open again by itself.
- Recent Screenshots is rebuilt from disk.
- **Reveal Last Screenshot** starts disabled: it means "the last capture in this session". **Show Last Screenshot Preview** uses the newest PNG on disk, so it works straight away.

### 4.5 An update is available
- The app checks GitHub in the background (§13).
- If a newer version exists, it asks once: install now, later, or skip this version.
- **Nothing is downloaded or installed until the user agrees.**

---

## 5. Capture

### 5.1 States

Only one capture runs at a time. Status text, menu items, the busy flag and cleanup must agree on every path.

| State | What happens | Next |
|---|---|---|
| Ready | Menu enabled; shortcuts can start a capture | Permission check |
| Permission needed (macOS) | Ask for access; explain Settings and reopening | Back to Ready; no automatic capture |
| Preparing | Make sure the folder exists; stop if it can't be created | Selecting |
| Selecting | Busy flag on; Capture Area disabled; app windows and preview hidden | Selection done, cancelled or failed |
| Processing | Check the image | Saved or error |
| Saved | Remember it as this session's last capture; enable Reveal Last | Copy |
| Saved and copied | Report success; show thumbnail if on | Ready |
| Saved, copy failed | Keep the file; show retry thumbnail if on; show the error | Ready |
| Cancelled / failed | Explain without claiming success | Ready |

### CAP-001 — Start a selection
- After the permission check (macOS) and folder preparation:
  - set the busy flag and disable **Capture Area**;
  - remember **the display that contains the pointer**;
  - hide the Welcome and Help windows and dismiss any preview;
  - set the status to "Select an area · Esc to cancel";
  - **wait 0.25 s** so menus close and the user can let go of the shortcut keys.

### CAP-002 — The selection itself
- Core:
  - The user drags a rectangle with the left mouse button, on **one display only** (the one with the pointer when the capture started).
  - **Esc** cancels. The pointer is not included in the image.
  - The saved image has the display's **full physical pixel resolution** (for example 2× on a Retina or 200 % display).
- **macOS:** run the system tool `/usr/sbin/screencapture` with the argument array `-i -s -x -t png <temp-path>`, never through a shell.
  - The temp file is `Screenshot-<UUID>.png` in the system temp folder.
  - Capture standard error. Don't block the main thread while waiting.
  - Platform difference: selection, Esc and Space behave as macOS defines them. The app does not draw its own selection screen.
- **Windows:** the app draws its own selection screen (the "overlay"):
  1. Grab the whole display with the pointer into memory (a **frozen frame**), after our own windows are hidden.
  2. Cover exactly that display with a borderless, topmost overlay window showing the frozen frame, dimmed (DESIGN §6). Other displays are left alone.
  3. Show a crosshair pointer. While dragging, the selected area shows undimmed, with a thin border and a size label ("1280 × 720", physical pixels).
  4. The selection is limited to the edges of that display.
  5. On release, crop the selection from the frozen frame and encode it as PNG in memory. Nothing is written to a temp file.
  6. **Esc** or a **right-click** cancels. A selection smaller than 2 × 2 physical pixels counts as cancelled.
  7. The overlay takes keyboard focus while it is open (so Esc works). When it closes, focus goes back to the window that had it before.
  8. The app must be **per-monitor DPI aware (v2)** so coordinates and pixels match on mixed-scale setups.

### CAP-003 — One at a time
- Ignore any trigger while the busy flag is set, including during the 0.25 s wait and while processing.
- A held-down or repeated shortcut must never open a second selection or produce a second file.

### CAP-004 — Always clean up
- When processing ends, on every path:
  - delete the temp file if there is one (best effort);
  - release the capture process or overlay;
  - clear the busy flag and re-enable Capture Area.
- If the capture can't start (macOS process launch fails, Windows display grab fails), restore Ready and show the error.
- **Quit** during a selection stops it: the macOS child process is terminated, the Windows overlay is closed.
- There is no startup sweep for temp files left by a crash (accepted limitation).

### CAP-005 — Cancel vs failure
- **macOS:**
  - If no temp file exists when `screencapture` exits:
    - check Screen Recording access again; if it's missing, show the permission message (§8);
    - otherwise, a non-zero exit with non-blank standard error shows "Capture could not finish. <detail>";
    - otherwise the status is "Capture cancelled".
  - If a temp file exists, check it and continue, even if the exit code was non-zero.
- **Windows:**
  - Esc, right-click or a tiny selection: "Capture cancelled".
  - A failed display grab or PNG encoding: "Capture could not finish. <detail>".

### CAP-006 — File first, then clipboard
- Save the PNG (§6). Only if the save succeeds:
  - remember it as this session's last capture;
  - then copy it (§7).
- Never show success, or the checkmark, before both steps have succeeded.

### CAP-007 — Copy failed after save
- Keep the saved file. **Reveal Last Screenshot** stays available.
- If thumbnails are on, show the preview with the caption "Saved · Click Copy to retry".
- Show the error (ERR-001). Never delete a good screenshot to make the result all-or-nothing.

### CAP-008 — Success
- Status: "Saved and copied to clipboard".
- If thumbnails are on, show the preview with the caption "Saved and copied".
- The status icon becomes a checkmark for about **2 s**, then returns to normal.
- If only the preview fails to load, the capture still counts as a success. The status explains the preview problem (PRE-006).

### ERR-001 — Errors the user can act on
- Operational errors open an alert:
  - title "Screenshotz needs attention", warning style;
  - the message says what failed and what to do.
- The menu status line also becomes "Screenshotz needs attention".
- Messages must say **which shortcut** failed, or that the image **was saved but not copied**.
- Expected failures:
  - the folder can't be created or written;
  - the image is empty or malformed;
  - a recent file was deleted or can't be read;
  - the image can't be decoded;
  - the clipboard can't be written;
  - a shortcut is already taken;
  - the Mark Up app is missing or won't open;
  - login registration fails;
  - (macOS) permission was revoked;
  - update check, download, safety check or install failed (§13).

---

## 6. Storage

### STO-001 — One fixed folder
- The folder is `screenshots`, lowercase, inside the user's Documents folder.
  - **macOS:** `~/Documents/screenshots`.
  - **Windows:** the *Documents* known folder plus `\screenshots`. Never hard-code `C:\Users\…`, because Documents is often moved into OneDrive.
- Create it, including missing parent folders, at launch, before each capture and when opening it.
- If it can't be created, show an error. **Never silently use a different folder.**

### STO-002 — Save the exact PNG
- Before saving, the image must:
  - decode to a bitmap with width and height above zero, **and**
  - start with the 8-byte PNG signature `89 50 4E 47 0D 0A 1A 0A`.
- Save the **exact bytes**. Never resize, re-compress or annotate, and never save the thumbnail in place of the original.

### STO-003 — Unique, safe names
- Format: `Screenshot yyyy-MM-dd at HH.mm.ss.SSS-<id>.png`.
  - Use the `en_US_POSIX` locale (invariant culture on Windows) and the local time zone.
  - `<id>` is a new UUID in upper case by default.
  - Example: `Screenshot 2026-11-01 at 14.03.27.125-550E8400-E29B-41D4-A716-446655440000.png`.
- If an id is passed in (tests), keep only letters, digits and hyphens, so it can never become a path.
- The name never contains `:` or `/` or `\`, so it is valid on both platforms.

### STO-004 — Never overwrite
- Write with **create-new** semantics. If a file with that exact name exists, the save fails and the existing file is untouched.
- Nothing is ever deleted automatically. There is no retention policy.

---

## 7. Clipboard

### CLP-001 — Put the image on the clipboard
- Put **image data** on the clipboard, never a file path, URL or text. Pasted size must equal the captured size.
- **macOS:** one `NSPasteboardItem` with `.png` (the original bytes) and `.tiff` (made from the decoded image), on the general pasteboard.
- **Windows:** the registered `PNG` format (the original bytes) **and** `CF_DIBV5` (32-bit, with alpha), in one clipboard update.
  - Office, Teams, browsers and Paint can all paste one of these.
  - If the clipboard is busy (another app has it open), retry up to **10 times, 50 ms apart**, then fail with an error.

### CLP-002 — Don't touch the clipboard on bad input
- Decode the image and prepare every format **before** clearing the clipboard.
- If the image can't be decoded, fail with an invalid-image error and leave the clipboard as it was.
- The copy helper accepts any decodable image. The PNG check is done by the save step, which always runs first for captures.

### CLP-003 — Report a failed write honestly
- Clear the clipboard right before writing. If the write fails, report it.
- The old clipboard contents are **not** restored after a failure at this last step. Docs and tests must not promise that they are.

---

## 8. Permission (macOS only)

### PER-001 — Screen Recording permission
- **macOS:** before each capture, call `CGPreflightScreenCaptureAccess`.
  - If access is denied, call `CGRequestScreenCaptureAccess`.
  - If it is still denied, show an alert with **Open System Settings** and **Later**. It explains *Privacy & Security → Screen Recording* (called *Screen & System Audio Recording* on newer macOS).
  - The link is `x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture`.
- **Windows:** not applicable; Windows has no screen-capture permission for desktop apps.
  - Platform difference: some protected video (DRM) may appear black in captures. The app doesn't detect this.

### PER-002 — Retry is the user's choice
- If access is granted during the request, say so and ask the user to press the shortcut again. **Never start a capture by itself.**
- Ask the user to reopen the app when macOS requires it.
- Never try to bypass approval or reset other apps' permissions.
- Because the app is signed with the same certificate every time (§14), **macOS keeps the permission across updates**.

---

## 9. Shortcuts

### KEY-001 — Two shortcuts, each with its own switch

| | macOS | Windows |
|---|---|---|
| Shortcut 1 | **⌥S** (Option-S), ID 2, preference `optionSDisabled`, **on** by default | **Alt+S**, ID 1, preference `altSDisabled`, **on** by default |
| Shortcut 2 | **⌘⌥4** (Command-Option-4), ID 1, preference `shortcutDisabled` (legacy name, keep it), **on** by default | **Print Screen**, ID 2, preference `printScreenEnabled`, **off** by default |
| API | Carbon `RegisterEventHotKey`, exclusive, shared callback, signature `0x53484F54` ("SHOT") | `RegisterHotKey` with `MOD_NOREPEAT`, on a hidden message window |

- Both apps register system hotkeys only. **Never watch or hook ordinary keystrokes.**
- Both shortcuts run the same capture as the menu command.

### KEY-002 — Saved choices, conflicts kept apart
- A missing preference means the default above.
- Register each enabled shortcut **separately**. If one fails:
  - name that shortcut and give the system error code;
  - still try the other one.
- Menu checkmarks show **what is actually registered**, not just the preference.
- The Capture Area item lists only active shortcuts (DESIGN §4).
- Both shortcuts can be off. Menu capture still works.
- **Windows, Print Screen:**
  - Before registering, read `HKCU\Control Panel\Keyboard\PrintScreenKeyForSnippingEnabled`.
  - If it is `1`, Windows uses the key for its own Snipping Tool. Don't register; show:
    > "Windows is using Print Screen for its own screen capture. Turn off **Use the Print screen key to open screen capture** in Settings, then turn this shortcut on again."
  - Show the alert with **Open Settings** (`ms-settings:easeofaccess-keyboard`) and **Cancel**. The menu item stays unchecked.

### KEY-003 — Events and cleanup
- Turning on a shortcut that's already on does nothing harmful.
- Turning one off unregisters only that one.
- Ignore events with an unknown ID or (macOS) a different signature, including ones queued after a key was turned off.
- At quit, unregister everything and remove the event handler or message window.
- **Typing trade-off (disclose in How to Use):**
  - While **⌥S** is registered, it no longer types its special character (ß on a US layout).
  - While **Alt+S** is registered, apps that use Alt+S (for example **Send** in Outlook) don't receive it. Ctrl+Enter still sends in Outlook.
  - Pressing Alt, letting go, then S (Office ribbon shortcuts) is not affected.

---

## 10. Launch at login and app lifecycle

### LOGIN-001 — Opt-in launch at login
- Off by default. **Never turned on automatically.** The system's state is the truth; the app keeps no flag of its own.
- Refresh the menu item each time the menu opens and after each change.
- **macOS:** use `SMAppService.mainApp`.

  | State | Menu item | Choosing it |
  |---|---|---|
  | enabled | "Launch at Login" ✓ | Unregister |
  | requiresApproval | "Launch at Login — Approval Needed…" | Open Login Items settings |
  | other | "Launch at Login" | Register |

- **Windows:** use the MSIX startup task (`Windows.ApplicationModel.StartupTask`, task ID `ScreenshotStartup`, off in the manifest).

  | State | Menu item | Choosing it |
  |---|---|---|
  | Enabled | "Launch at Login" ✓ | Disable |
  | DisabledByUser | "Launch at Login — Turn On in Settings…" | Open `ms-settings:startupapps` |
  | DisabledByPolicy | "Launch at Login" (disabled) | — |
  | Disabled | "Launch at Login" | `RequestEnableAsync` |

### LIFE-001 — One copy, menu-bar or tray only
- No Dock icon or taskbar button.
  - **macOS:** accessory app (`LSUIElement = true`).
  - **Windows:** tray icon only; no main window; app windows don't show in the taskbar.
- **Only one copy runs.** A second launch exits after telling the first copy to show the Welcome window.
  - **macOS:** detect another running app with the same bundle ID; reopening the app shows Welcome.
  - **Windows:** a named mutex `Local\AshwinMathias.Screenshot`. The second copy signals the first through a registered window message, then exits.
- First launch shows Welcome and sets `hasLaunched = true`. Later launches don't show it by themselves.
- Closing the last window doesn't quit the app.
- **Quit** dismisses the preview, unregisters shortcuts, stops an active capture, and cancels an update download.
- **Windows tray:**
  - A **left-click or right-click** on the tray icon opens the same menu.
  - The tooltip is "Screenshotz — <status>".
  - On first launch, Welcome tells the user that Windows may put the icon in the hidden-icons area (^) and how to pin it.

---

## 11. Menu and windows

### UI-001 — Menu (same order on both platforms)

```
<status line, disabled>
[Install Update <version>…]            ← only while an update is waiting (§13)
──────────
Capture Area (<active shortcuts>)
Open Screenshots Folder
Reveal Last Screenshot                 ← disabled until a capture this session
Recent Screenshots ▸
Show Last Screenshot Preview
──────────
Show Thumbnail After Capture           ✓ by default
Enable ⌥S Shortcut        | Enable Alt+S Shortcut
Enable ⌘⌥4 Shortcut       | Enable Print Screen Shortcut
Launch at Login
Check for Updates Automatically        ✓ by default
──────────
Screen Recording Permission…           ← macOS only
Check for Updates…
How to Use Screenshotz…
Help & About Screenshotz…
──────────
Quit Screenshotz                        ⌘Q on macOS
```

- First status: "Ready to capture".
- On Windows, "Reveal" items say **File Explorer** where macOS says **Finder** (see §12).

### UI-002 — Welcome window
- Centred, with a title bar, closable. Size and layout are in DESIGN §7.
- Headline: "A screenshot. In two places."
- Shows the **active shortcuts** large.
- Explains:
  - selecting an area;
  - where files are saved;
  - that the image is on the clipboard;
  - Esc to cancel;
  - that each shortcut can be turned off;
  - **Windows only:** how to pin the tray icon.
- Buttons: **Capture Area**, **Recent Screenshots**, **Help & About**. A small status line under them shows the live status.

### HELP-001 — Help & About window
- Three tabs: **About**, **How to Use**, **Release History**. Text wraps, scrolls, can be selected and can't be edited.
- **About** (top to bottom):
  1. App icon, "Screenshotz", "Version 0.5.0 (build N)" read from the app's own metadata.
  2. **Author row:** avatar (circle) · **Ashwin Mathias** · "Creator of Screenshotz". The text comes from `about.json`.
  3. **Sparky card:**
     - Sparky's photo (circle).
     - "Designed with love for **Sparky**, who always camps out where I work."
  4. macOS: "Screenshots stay on this computer. Screenshotz only goes online to check GitHub for updates."
     Windows Store build: "Screenshots stay on this computer. Screenshotz doesn't go online; the Microsoft Store handles updates."
  5. **Check for Updates…** button.
  6. "MIT licence · github.com/ashwin-mathias/Screenshots" (link opens the browser).
- There are no payment or QR codes.

### HELP-002 — How to Use and Release History
- **How to Use** covers:
  - capture and paste, and Esc;
  - file names and the folder;
  - "last in this session" vs "newest on disk";
  - preview hover and buttons;
  - Recent Screenshots;
  - shortcuts and the typing trade-off (KEY-003);
  - (macOS) permission recovery and the Control-key note (§11.1);
  - (Windows) Print Screen setup and pinning the tray icon;
  - launch at login, updates, closing vs quitting.
- **Release History** reads the bundled `changelog.json`, newest first:
  - each entry shows "Version X.Y.Z", the date, a **LATEST** tag on the first, the title and bullet notes;
  - if the file is missing, show "Release history isn't available in this build."

### 11.1 Things the user should know about selection (put in How to Use)
- **macOS:**
  - Let go of ⌘ and ⌥ before dragging.
  - Don't hold **Control**: macOS then sends the image to the clipboard only and skips the file. The app can't prevent this.
  - The built-in ⇧⌘4 still works and isn't affected.
- **Windows:**
  - Let go of Alt before dragging.
  - Selection stays on the display where the pointer was.
  - Win+Shift+S (Windows' own tool) still works and isn't affected.

---

## 12. Recent screenshots and file actions

### REC-001 — Read the folder fresh
- At launch and each time Recent Screenshots opens, list the folder **on a background thread**.
- Only one refresh at a time. Update the menu on the main thread.
- While loading with nothing shown yet: "Loading screenshots…".
- No polling, no database, no copying into a library, no deleting.

### REC-002 — Which files, in what order
- Include **regular files** whose extension is `.png` (any case) directly in the folder. No subfolders.
- Exclude:
  - hidden files: names starting with `.`, and on Windows the Hidden or System attribute;
  - folders;
  - links: symlinks, plus Windows reparse points and junctions.
- Sort by modification date, **newest first**. A missing date counts as the oldest possible. Ties sort by file name A→Z (ordinal, case-sensitive).
- Return the first **20** by default, or the given positive limit. A limit of 0 or less returns nothing.
- Files are chosen by name and metadata, not by checking the image. A corrupt `.png` can appear, and actions must handle it. Any PNG counts, whoever created it.

### REC-003 — Readable names
- Show the file name without `.png`, and without a trailing `-<UUID>` if it has one.
- Tooltip: the full file name and the local modification date and time.
- Each file has its own submenu of actions.
- Empty folder: "No screenshots yet". Read error: "Could not read screenshots folder".
- After loading, the last item is always **Open Screenshots Folder**.

### ACT-001 — Check the file again before acting
- Before Copy, Open, Reveal or Mark Up, check that the path is still a regular file.
- If it was moved or deleted, show an error naming the action ("Couldn't copy that screenshot. It may have been moved or deleted.").
- This is not a defence against someone swapping files on purpose, and docs must not say it is.

### ACT-002 — The actions

| Action | Core behaviour | macOS | Windows |
|---|---|---|---|
| **Open** | Open the exact file in the default image app; report failure | `NSWorkspace.open` | Shell "open" |
| **Copy Image** | Read the file and copy it (§7). Status "Copied screenshot to clipboard" | — | — |
| **Reveal** | Show the folder with the file selected | "Reveal in Finder" | "Show in File Explorer" (`SHOpenFolderAndSelectItems`) |
| **Show Preview** | Open the preview with the caption "Recent screenshot" | — | — |
| **Mark Up** | Open the exact file in the system editor, which has the drawing tools | "Mark Up in Preview…" (`com.apple.Preview`); tell the user the Markup toolbar is in Preview | "Mark Up in Paint…" (the PNG "edit" verb, Paint by default) |

- If the editor is missing or fails to open, explain it.

### REC-004 — Two meanings of "last"
- **Reveal Last Screenshot** = the last file saved by a capture **in this session**. It starts disabled.
- **Show Last Screenshot Preview** = the **newest PNG on disk** (limit 1, background), even when thumbnails are off. An older file that was edited can become "newest".

---

## 13. Updates

UPD-001 to UPD-006 describe the **in-app updater**, used by the **macOS app** and the **Windows test build**. The **Windows Store build** uses the Microsoft Store instead (UPD-007).

### UPD-001 — When the app checks
- **Check for Updates Automatically** is **on** by default (preference `autoUpdateCheckDisabled`, missing = on).
  - When on, check **10 s after launch** and then every **24 h** while running.
  - The last check time is saved (`lastUpdateCheck`), so a restart doesn't cause an extra check within 24 h.
- **Check for Updates…** (menu and About) checks at once.
- Automatic checks are **silent** unless there's an update. Failures are only logged.
- A manual check always answers:
  - "You're up to date. Screenshotz 0.5.0 is the latest version.", or
  - "Couldn't check for updates. <detail>", or
  - the update prompt.

### UPD-002 — What is checked
- `GET https://api.github.com/repos/ashwin-mathias/Screenshots/releases/latest`
  - Headers: `Accept: application/vnd.github+json`, `User-Agent: Screenshotz/<version> (<macOS|Windows>)`.
  - No login or token. Timeout 15 s.
- Read `tag_name` (format `vX.Y.Z`), `body` (release notes) and `assets`.
- An update exists when the tag version is **greater** than the running version, comparing X, Y and Z as numbers.
  - Tags in any other format are ignored.
  - Drafts and pre-releases are never offered.
- The platform's asset must be present **with its checksum file**, or the release is ignored on that platform:

  | Platform | App file | Checksum file |
  |---|---|---|
  | macOS | `Screenshotz-mac.zip` | `Screenshotz-mac.zip.sha256` |
  | Windows | `Screenshotz-win-x64.msix` | `Screenshotz-win-x64.msix.sha256` |

- Asset links must start with `https://github.com/ashwin-mathias/Screenshots/releases/download/`. Redirects are followed only to `https` addresses.

### UPD-003 — Ask first, always
- When an update is found:
  - show the prompt (DESIGN §9): "Screenshotz <new> is available" / "You have <current>." plus the release notes;
  - add the menu item **Install Update <new>…** under the status line.
- Buttons:
  - **Install and Relaunch** (default) — download, check and install.
  - **Later** — close; the menu item stays; the next automatic check may ask again.
  - **Skip This Version** — saved as `skippedVersion`. Automatic checks don't offer that version again. A manual check still does.
- **Nothing is downloaded before the user chooses Install.**
- An update never starts during a capture. If a capture is running, it waits until the capture ends.

### UPD-004 — Download and safety check
- Download to a new temp folder. While downloading:
  - the status line shows "Downloading update… 42%";
  - the menu shows **Cancel Update** in place of the install item.
- Download the `.sha256` file too. It must hold exactly 64 hex characters (anything after the first whitespace is ignored).
- **Safety checks, all required:**
  1. The SHA-256 of the download equals the checksum file.
  2. **The download is signed with the same certificate as the running app.**
     - **macOS:** after unzipping with `ditto -x -k`:
       - `codesign --verify --strict --deep` must pass;
       - the bundle ID must be `local.ashwinmathias.Screenshot`;
       - the leaf signing certificate's SHA-256 must equal the running app's.
     - **Windows:**
       - the package's Authenticode signature must be valid;
       - the signer certificate's thumbprint must equal the thumbprint built into the app (§14);
       - the package identity name must be `AshwinMathias.Screenshot`.
  3. The version inside the download equals the offered version.
- If any check fails:
  - delete the download;
  - keep the current version;
  - show: "The update didn't pass its safety check, so it wasn't installed. <detail>".
- The checksum catches corrupt or mixed-up downloads. **The certificate check is the real protection.**

### UPD-005 — Install and relaunch
- **macOS:**
  1. Check that the folder holding `Screenshotz.app` is writable. If it isn't, say "Screenshotz can't update itself in this folder. Move it to Applications, or download the update yourself." with **Open Download Page** and **Cancel**.
  2. Start a small helper script (`/bin/sh` with an argument array) and quit. The script:
     - waits for the app's process to end;
     - moves the old app to the temp folder as a backup;
     - moves the new app into place;
     - opens it;
     - if any step fails, puts the backup back and opens the old app.
  3. Files downloaded by the app itself aren't quarantined, so macOS shows **no "Open Anyway" prompt** for updates.
- **Windows:**
  1. Call `RegisterApplicationRestart` so Windows restarts the app after the update.
  2. Install with `PackageManager.AddPackageAsync(<msix>, null, ForceApplicationShutdown)`. Windows closes the app, updates it and restarts it.
  3. On failure (for example the certificate isn't trusted on this PC), show the Windows error in plain words with **Open Download Page**.
- After an update, the app opens normally. Settings, shortcuts, and (macOS) the Screen Recording permission are kept.
- Status after relaunch, once: "Updated to Screenshotz <version>".

### UPD-006 — Network limits
- The app talks only to `api.github.com`, `github.com` and GitHub's download servers it is redirected to, and only for UPD-002 and UPD-004.
- No analytics, no IDs, no screenshot data, ever.

### UPD-007 — Windows: the Microsoft Store updates the app (platform difference)
- The Store build contains **no update code paths that run**: no GitHub checks, no downloads. It makes **no network requests at all**.
- The menu has no **Check for Updates Automatically** item and never shows **Install Update…**.
- **Check for Updates…** (menu and About) opens the app's Store page (`ms-windows-store://pdp/?productid=9NM6DV5TJJ6C`). The Store shows **Update** there when a newer version exists.
- Windows installs Store updates by itself. Settings and shortcuts are kept, because the package identity never changes.
- The build channel (`store` or `test`) is fixed at build time, not detected at run time.

---

## 14. Releases and trust (summary)

Details are in ARCHITECTURE §8.

- Every push to `main` builds and tests both apps on GitHub Actions.
- A **release is published only when `VERSION` changes** to a version with no tag yet. Tag `vX.Y.Z`. Assets:
  - `Screenshotz-mac.zip` and `Screenshotz-mac.zip.sha256` (the macOS updater reads these);
  - `Screenshotz-win-x64.msix`, `Screenshotz-win-x64.msix.sha256` and `Screenshotz-signing.cer`: the Windows **test build**, its checksum and its public certificate;
  - `Screenshotz-store-x64.msixupload`: the unsigned **Store build**, which Ashwin uploads in Partner Center (see STORE.md);
  - The release notes are the `changelog.json` entry for that version.
- The macOS app and the Windows test build are signed in CI with **self-signed certificates** kept in GitHub Actions secrets, with an offline backup kept by Ashwin. Microsoft signs the Store build.
- **First install:**
  - **macOS** (once per Mac): unzip, move `Screenshotz.app` to Applications, open it. macOS blocks the first open; allow it in **System Settings → Privacy & Security → Open Anyway**.
  - **Windows:** **Get** it from the Microsoft Store. No other step.
  - **Windows test build** (Ashwin only): trust `Screenshotz-signing.cer` once (**Local Machine → Trusted People**), then double-click the `.msix`. Test and Store builds have different identities, so both can be installed side by side.
- Updates after that need no extra steps (§13).

---

## 15. Preview (floating thumbnail)

### PRE-001 — Never takes focus
- The preview floats above other windows and **never takes keyboard focus**. Typing continues in the app the user was using.
- It stays visible when other apps are active, appears on every desktop or Space, and doesn't appear in Alt-Tab or ⌘-Tab.
- **macOS:**
  - a borderless, non-activating `NSPanel` that can't become key or main;
  - set `canJoinAllSpaces` and `fullScreenAuxiliary`, and ignore it in window cycling;
  - never activate the app just to show it.
- **Windows:**
  - a borderless topmost window with the extended styles `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST`, shown without activation;
  - buttons work on click without taking focus.

### PRE-002 — Layout
- 324 × 248 points (DIPs on Windows). The full layout is in DESIGN §5.

### PRE-003 — Position
- On **the display where the capture started**, 20 pt from the right and bottom edges of its usable area (above the Dock, or above the taskbar on Windows).
- If no display was remembered, use the main display, then the first.
- Manual previews reuse the last remembered display (accepted limitation).

### PRE-004 — Small, bounded decoding
- Decode a thumbnail no larger than **640 px** on its longest side, respecting image orientation, and keep no full-size copy.
  - **macOS:** ImageIO thumbnail with no full-image caching.
  - **Windows:** WIC with a scaled decode.
- A requested maximum of 0 or less returns no thumbnail. The source file is never changed.

### PRE-005 — Lifetime and actions
- Close automatically **8 s** after it appears.
- Moving the pointer into it cancels the timer. Moving out starts a **fresh** 8 s, not what was left.
- **✕** closes at once.
- **Copy**, **Reveal** and **Mark Up** do their action (ACT-002) and then close.
- Starting a new capture closes it before selection.
- Closing releases the image and cancels the timer.

### PRE-006 — Automatic thumbnails can be turned off
- **Show Thumbnail After Capture** is on by default (preference `capturePreviewDisabled`, missing = on).
- Turning it off closes any open preview. Manual preview commands still work.
- If the thumbnail can't be decoded, there's no preview and the status is "Screenshot preview unavailable · Use Recent Screenshots to open it".

---

## 16. Settings and stored data

| Key | Type / default | Meaning | Platforms |
|---|---|---|---|
| `hasLaunched` | Bool / false | Welcome was shown once | Both |
| `optionSDisabled` | Bool / false | ⌥S turned off | macOS |
| `shortcutDisabled` | Bool / false | ⌘⌥4 turned off (legacy name) | macOS |
| `altSDisabled` | Bool / false | Alt+S turned off | Windows |
| `printScreenEnabled` | Bool / false | Print Screen turned on | Windows |
| `capturePreviewDisabled` | Bool / false | Automatic thumbnails off | Both |
| `autoUpdateCheckDisabled` | Bool / false | Automatic update checks off | Both |
| `lastUpdateCheck` | Date / none | Last successful or failed check | Both |
| `skippedVersion` | String / none | Version the user skipped | Both |
| Launch at login | System-owned | Not stored by the app | Both |
| Last capture | In memory only | This session's last saved file | Both |

- **macOS:** `UserDefaults` for the bundle ID. The keys are compatible with the earlier 0.4 app, so existing choices carry over.
- **Windows:** `settings.json` in the app's local data folder.
  - Missing or unreadable means defaults; the bad file is renamed to `settings.bad.json`.
  - Writes go to a temp file first, then replace the original.
- Screenshots are ordinary files and stay when the app is removed.

## 17. Quality, privacy and accessibility

- **NFR-001 Local first:**
  - no telemetry or accounts;
  - capture only on the user's command;
  - network only as in UPD-006;
  - no Accessibility or Input Monitoring permission (macOS), and no keyboard hooks (Windows).
  - The app doesn't control whether the OS syncs Documents (iCloud, OneDrive).
- **NFR-002 Protect user data:**
  - never overwrite, auto-delete or alter originals;
  - tests use generated images, temp folders and a private clipboard (macOS) or a test clipboard wrapper (Windows), never the user's real files or clipboard;
  - no screenshots, settings or certificates in the repo.
- **NFR-003 Responsive and readable:**
  - captures and folder reads never block the UI thread;
  - native system fonts and colours;
  - light and dark modes follow the system;
  - nothing is clipped at 100–200 % scaling.
- **NFR-004 Bounded memory:** thumbnails are at most 640 px; recents store paths and dates only.
- **NFR-005 User in control:**
  - closing windows keeps the app running and Quit stops it;
  - login launch, shortcuts, thumbnails and update checks can all be turned off;
  - permission denial never causes repeated automatic captures;
  - updates install only after the user agrees.
- **NFR-006 Accessibility:**
  - meaningful names for the status icon ("Screenshotz"), preview image ("Captured screenshot"), close button ("Dismiss preview"), preview buttons and help tabs;
  - everything works with VoiceOver and Narrator;
  - every window works from the keyboard;
  - the Windows overlay is the exception: it is mouse-only for selection, and Esc cancels.
