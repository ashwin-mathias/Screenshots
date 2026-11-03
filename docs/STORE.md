# Screenshotz — Microsoft Store submission

Everything needed to publish and update the Windows app in Partner Center (SPEC §14, UPD-007). Text marked **Paste** is ready to copy.

## Identity (from Partner Center → Product identity)

| | |
|---|---|
| Package/Identity/Name | `AshwinMathias.Screenshotz` |
| Package/Identity/Publisher | `CN=7E8438D5-12B0-4FC0-9098-FE994BBCE149` |
| PublisherDisplayName | `Ashwin Mathias` |
| Package Family Name | `AshwinMathias.Screenshotz_gg9r6ndhn3xw6` |
| Store ID | `9NM6DV5TJJ6C` |

These are built into the Store package by `build-msix.ps1 -Channel store`. None of them are secret.

## Each release

1. Get `Screenshotz-store-x64.msixupload`:
   - from the GitHub release `vX.Y.Z`, or
   - from a CI run's artifact `Screenshotz-store-build-N`.
2. Go to Partner Center → **Apps and games → Screenshotz → Start update** (the first time: **Start your submission**).
3. **Packages:** drag in the `.msixupload`. Wait for validation to pass. Remove the previous package only if Partner Center asks you to.
4. **Store listings → English:** update **What's new in this version** from `shared/changelog.json`.
5. **Submit for certification.** It usually takes 1–3 business days. Windows then updates installed copies by itself.

## First submission: section by section

### Pricing and availability
- Markets: all, or the ones you choose.
- Visibility: **Public audience**. To test first, choose **Private audience** and add your own email.
- Pricing: **Free**. No free trial.

### Properties
- Category: **Utilities + tools**. Subcategory, if offered: none.
- Privacy policy URL: **Paste** `https://github.com/ashwin-mathias/Screenshots/blob/main/PRIVACY.md`
- Website: **Paste** `https://github.com/ashwin-mathias/Screenshots`
- Support contact: **Paste** `https://github.com/ashwin-mathias/Screenshots/issues`
- Product declarations:
  - ☐ accessibility guidelines: leave unticked until the Narrator checks in ACCEPTANCE F5 pass;
  - ☐ customers can install to alternate drives: leave unticked;
  - ☐ depends on non-Microsoft drivers or NT services: leave unticked.
- System requirements: Minimum: **Keyboard** and **Mouse**. The selection is made with the mouse.

### Age ratings
- Category: **Utility**.
- Answer **No** to every question: no user-generated content shared, no chat, no location sharing, no purchases, no violence and so on. Screenshotz has no online features.
- Expected result: rated for everyone (IARC 3+ / ESRB E).

### Packages
- Upload `Screenshotz-store-x64.msixupload`. Device family: **Windows 10/11 Desktop**.

### Submission options → Restricted capabilities
- The package declares `runFullTrust`. Every classic desktop app needs it.
- **Paste:**
  > Screenshotz is a desktop app (WPF / .NET) packaged as MSIX. runFullTrust is required to register its global keyboard shortcut (RegisterHotKey), to show its notification-area icon (Shell_NotifyIcon), to capture the screen area the user selects, and to write the image to the clipboard and the user's Documents\screenshots folder. It makes no network requests and collects no data.

### Store listing (English)

**Description (Paste):**
```
Screenshot it. Paste it. Keep it.

Press Alt+S, drag across anything on your screen, and Screenshotz does two things at once: it saves a crisp PNG to your Documents\screenshots folder and puts the image on your clipboard. The moment you let go of the mouse, it's ready to paste into an email, a chat or a slide.

No editor popping up. No "Save as" box. No hunting for where the file went.

WHY YOU'LL LOVE IT
• One shortcut, two results: an image ready to paste right now, and a file you'll still have next month
• Never breaks your flow: a small preview slides into the corner without taking your cursor, so you keep typing and paste straight away
• Find it again in one click: Recent Screenshots lists your newest 20 captures, each with Copy, Open, Show in File Explorer and Mark Up
• Sharp on every monitor: captured at full resolution, whatever the display's scaling
• Your files are safe: every capture gets its own name, and nothing is ever overwritten or deleted
• Mark it up when you need to: open any screenshot in Paint with one click
• Your shortcut, your way: Alt+S by default, add Print Screen if you prefer, or turn either off
• Out of the way: lives quietly in the system tray and can start when you sign in

PRIVATE BY DESIGN
No account. No ads. No tracking. Screenshotz never connects to the internet. Your screenshots stay on your PC.

PERFECT FOR
• Pasting snippets into Teams, Slack, Outlook and Word
• Saving receipts, confirmations and error messages
• Building slides, documents and bug reports
• Anyone who takes more than a few screenshots a day

Made with love for Sparky, who always camps out where I work.
```

**What's new in this version** from `shared/changelog.json`.
5. **Submit for certification.** It usually takes 1–3 business days. Windows then updates installed copies by itself.

## First submission: section by section

### Pricing and availability
- Markets: all, or the ones you choose.
- Visibility: **Public audience**. To test first, choose **Private audience** and add your own email.
- Pricing: **Free**. No free trial.

### Properties
- Category: **Utilities + tools**. Subcategory, if offered: none.
- Privacy policy URL: **Paste** `https://github.com/ashwin-mathias/Screenshots/blob/main/PRIVACY.md`
- Website: **Paste** `https://github.com/ashwin-mathias/Screenshots`
- Support contact: **Paste** `https://github.com/ashwin-mathias/Screenshots/issues`
- Product declarations:
  - ☐ accessibility guidelines: leave unticked until the Narrator checks in ACCEPTANCE F5 pass;
  - ☐ customers can install to alternate drives: leave unticked;
  - ☐ depends on non-Microsoft drivers or NT services: leave unticked.
- System requirements: Minimum: **Keyboard** and **Mouse**. The selection is made with the mouse.

### Age ratings
- Category: **Utility**.
- Answer **No** to every question: no user-generated content shared, no chat, no location sharing, no purchases, no violence and so on. Screenshotz has no online features.
- Expected result: rated for everyone (IARC 3+ / ESRB E).

### Packages
- Upload `Screenshotz-store-x64.msixupload`. Device family: **Windows 10/11 Desktop**.

### Submission options → Restricted capabilities
- The package declares `runFullTrust`. Every classic desktop app needs it.
- **Paste:**
  > Screenshotz is a desktop app (WPF / .NET) packaged as MSIX. runFullTrust is required to register its global keyboard shortcut (RegisterHotKey), to show its notification-area icon (Shell_NotifyIcon), to capture the screen area the user selects, and to write the image to the clipboard and the user's Documents\screenshots folder. It makes no network requests and collects no data.

### Store listing (English)

**Description (Paste):**
```
Screenshotz captures an area of your screen and puts it in two places at once: a PNG file in Documents\screenshots, and an image on your clipboard, ready to paste.

Press Alt+S, drag across what you want, and paste it into Word, Teams, Outlook, Slack or anywhere else that takes images. The original file is kept, never overwritten and never uploaded.

• One shortcut, two results: a file you keep and an image you can paste straight away
• A small preview in the corner that never steals your typing focus, with Copy, Reveal and Mark Up
• Recent Screenshots: your newest 20 captures, one click away
• Works across displays with different scaling; the image is always captured at full resolution
• Optional Print Screen shortcut
• Lives quietly in the system tray, and can start when you sign in
• Private: no account, no tracking, no network access

Made with love for Sparky, who always camps out where I work.
```

**What's new in this version (Paste for 0.5.0):**
```
First release on Windows: capture with Alt+S, save and copy in one step, floating preview, Recent Screenshots, and an optional Print Screen shortcut.
```

**Product features (Paste, one per line):**
```
Alt+S: select an area, saved as a PNG and copied to your clipboard in one step
Floating preview that never takes your typing focus
Recent Screenshots: your newest 20 captures, one click away
Full-resolution captures on every monitor, whatever its scaling
Nothing is ever overwritten or deleted
Mark up any screenshot in Paint with one click
Optional Print Screen shortcut
Private: no account, no ads, no tracking, no internet connection
```

**Short description (Paste):** `Screenshot it. Paste it. Keep it. Press Alt+S: your selection is saved as a PNG and copied to your clipboard, ready to paste.`

**Keywords (Paste, up to 7):** `screenshot`, `screen capture`, `snipping`, `clipboard`, `PNG`, `snip`, `capture`

**Copyright (Paste):** `© 2026 Ashwin Mathias`

**Screenshots:** at least 1, up to 10, PNG, 1366 × 768 or larger. Take them on your PC:
1. The selection screen while dragging, with the size label showing.
2. The preview card in the corner after a capture.
3. The tray menu open, with Recent Screenshots.
4. Help & About, showing the Sparky card.

**Store logos** (Store listings → Store logos), all in `shared/assets/icon/store/` and made by `tools/make-icons.py`:

| Partner Center slot | File |
|---|---|
| 1:1 box art (shown on the Store page; recommended) | `Screenshotz-box-art-2160x2160.png` |
| 2:3 poster art (optional) | `Screenshotz-poster-art-1440x2160.png` |
| 1:1 app tile icon, 300 × 300 (optional) | `Screenshotz-tile-icon-300x300.png` |

The icons in Start and the taskbar come from the package itself, so nothing else needs uploading.
