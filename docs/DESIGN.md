# Screenshot — Design

How the app **looks**. The apps use each platform's own controls, fonts and colours, so they feel native. Sizes and layout are shared so the two apps feel like the same product. If code and this file disagree, fix one of them in the same commit.

---

## 1. Principles

- **Quiet and native.** System fonts, system colours, system controls. No custom theme, no brand colour inside the UI, except the app icon.
- **Small.** The app is mostly invisible: an icon, a menu, and a preview for a few seconds.
- **Light and dark** follow the system on both platforms.
- **Units:**
  - macOS sizes are **points**;
  - Windows sizes are **DIPs** (device-independent pixels, 1/96 inch);
  - the same number is used on both.

## 2. Typography

| Role | macOS | Windows | Size | Weight |
|---|---|---|---|---|
| Body / menu | SF Pro (system) | Segoe UI Variable (system) | system default | Regular |
| Preview caption | system | system | 12 | Medium |
| Welcome headline | system | system | 26 | Semibold |
| Welcome shortcut keys | system | system | 20 | Semibold |
| Window section labels | system | system | 13 | Semibold |
| Secondary text | system | system | 12 | Regular, secondary colour |

## 3. Colours

- Use **semantic system colours only**:
  - macOS: `labelColor`, `secondaryLabelColor`, `windowBackgroundColor`, `controlAccentColor`, and so on;
  - Windows: the WPF Fluent theme resources, which follow the system accent and light/dark mode.
- **Exceptions:**
  - the app icon gradient (§10);
  - the Windows selection overlay (§6), which must look the same on any theme.

## 4. Status icon and menu

- **macOS menu bar:**
  - the SF Symbol `viewfinder`, template image, accessibility label "Screenshot";
  - on success, `checkmark` for 2 s.
- **Windows tray:**
  - the monochrome viewfinder glyph from §10 (16 / 20 / 24 / 32 px), white for a dark taskbar and black for a light one;
  - switch when the theme changes (`SystemUsesLightTheme`);
  - on success, a checkmark icon for 2 s;
  - tooltip "Screenshot — <status>".
- **Menu:**
  - macOS: native `NSMenu`;
  - Windows: a WPF `ContextMenu` with the Fluent theme, opened at the pointer from the tray icon. The item order is in SPEC UI-001.
- **Capture Area label:**
  - macOS: "Capture Area (⌥S or ⌘⌥4)", "Capture Area (⌥S)", "Capture Area (⌘⌥4)", or just "Capture Area".
  - Windows: "Capture Area (Alt+S or Print Screen)", "Capture Area (Alt+S)", "Capture Area (Print Screen)", or just "Capture Area".
- macOS labels use the symbols ⌘ ⌥ ⇧ ⌃. Windows labels use words joined by "+".

## 5. Preview (floating thumbnail)

```
┌───────────────────────────────────────── 324 ─┐
│ 14                                       ✕    │  ← close: 18×18, 10 from top/right
│   ┌───────────────────────────────────────┐   │
│   │                                       │   │
│   │   image, fit inside, 150 high,        │   │
│   │   corners 6                           │   │
│   └───────────────────────────────────────┘   │
│   Saved and copied                (12 medium) │  ← caption, middle-truncated
│   [ Copy ]  [ Reveal ]  [ Mark Up ]           │  ← small native buttons, spacing 8
└───────────────────────────────────────── 248 ─┘
```

- Size **324 × 248**. Side margins **14**, top **14**, bottom **14**.
- Image area: full width minus margins, **150** high, aspect fit, **6** corner radius.
- The caption and its tooltip show the full file name.
- Buttons:
  - **Copy**, **Reveal** and **Mark Up** (same words on both platforms);
  - the tooltips are "Copy image", "Reveal in Finder" / "Show in File Explorer", and "Mark Up in Preview" / "Mark Up in Paint".
- Background:
  - **macOS:** `NSVisualEffectView`, material `.popover`, outer corner radius **14**, shadow, clear window background.
  - **Windows:** Fluent `LayerFillColorDefault` over Mica-alt where available, otherwise the solid theme background; corner radius **8** (the Windows 11 standard); a 1-DIP `CardStrokeColorDefault` border; standard shadow.
- Accessibility names:
  - the image: "Captured screenshot";
  - ✕: "Dismiss preview";
  - the buttons: their labels.

## 6. Windows selection overlay

- Covers **one display** exactly, at its physical resolution.
- **Frozen frame** with a black dim of **40 %** opacity.
- **Selected area:**
  - shown at full brightness (no dim);
  - border **1 px white**, inside a **1 px black at 50 %** outline, visible on any content.
- **Size label:**
  - "1280 × 720" in physical pixels;
  - Segoe UI 12, white on black at 70 %, corner radius 4, padding 6 × 3;
  - sits 8 px below the bottom-right corner of the selection, and moves inside the selection if there's no room.
- Pointer: the standard crosshair.
- No toolbar, no buttons, no instructions on screen. The status line already says "Select an area · Esc to cancel".

## 7. Welcome window

- **540 × 400**, centred, title "Welcome to Screenshot".
- Not resizable.
  - macOS: closable and minimisable.
  - Windows: closable only.
- Side margins **28**, top **26**.
- Content, top to bottom:
  1. Headline "A screenshot. In two places." (26 semibold).
  2. Sub-line: "Select an area. It's saved as a PNG and copied to your clipboard, ready to paste." (secondary).
  3. Shortcut row: each active shortcut shown as key caps (20 semibold in a rounded rectangle), with "or" between them.
     - macOS: ⌥ S and ⌘ ⌥ 4.
     - Windows: Alt + S and Print Screen.
     - If both are off: "Use Capture Area in the menu."
  4. Four short lines:
     - "Drag to select · Esc cancels"
     - "Saved to Documents/screenshots" (Windows: `Documents\screenshots`)
     - "The image is on your clipboard"
     - "Turn shortcuts on or off in the menu"
  5. **Windows only:** "Can't see the icon? Click ^ in the taskbar and drag Screenshot next to the clock."
  6. Buttons, right-aligned: **Help & About**, **Recent Screenshots**, **Capture Area** (default / accent).
  7. Live status line (12, secondary) under the buttons.

## 8. Help & About window

- **620 × 540**, minimum **520 × 430**, centred, resizable. Tabs: **About · How to Use · Release History**.
- Text scrolls, wraps and can be selected. Margins 24.
- **About tab:**
  - **App row:**
    - app icon 64;
    - "Screenshot" (20 semibold);
    - "Version 0.5.0 (build N)" (12 secondary).
  - **Author row** (as in couch-lab):
    - avatar **64**, circle (`avatar.png`), gap 16;
    - **Ashwin Mathias** (17 semibold);
    - "Creator of Screenshot" (12 secondary).
  - **Sparky card:**
    - rounded rectangle, corner radius 12, padding 16;
    - background: macOS `controlBackgroundColor`, Windows `CardBackgroundFillColorDefault`;
    - Sparky's photo **72**, circle (`sparky.jpg`), gap 16, then the note text (13, wraps).
  - **Privacy line** (12 secondary).
  - **Check for Updates…** button.
  - Licence and repo link (12 secondary; the link uses the accent colour).
- **Release History tab:** one card per version, newest first.
  - "Version 0.5.0" (15 semibold), with a small **LATEST** tag (accent outline) on the first;
  - date (12 secondary), title (13 semibold), notes as bullets (13).

## 9. Update prompt

- **macOS:** `NSAlert`, informational style, app icon.
  - Message: "Screenshot 0.6.0 is available".
  - Informative text: "You have 0.5.0. Would you like to install it now?"
  - Accessory view: a scrolling, read-only text view, 400 × 160, showing the release notes.
  - Buttons: **Install and Relaunch** (default), **Later**, **Skip This Version**.
- **Windows:** a small WPF dialog, 480 × 360, with the same text, notes area and buttons. It's a normal window: it takes focus when it opens, because the user has to choose.
- Download progress is shown in the menu status line, not a separate window.

## 10. App icon

- A rounded square tile with a **blue-to-teal diagonal gradient**:
  - top left `#2F6FE4`;
  - bottom right `#17B3A8`.
- Four **white capture corners** (L-shapes) and a small white **crosshair** in the centre. Subtle drop shadow.
- **Sizes:**
  - macOS: 16, 32, 128, 256 and 512 at 1× and 2× in `AppIcon.icns`;
  - Windows: an `.ico` with 16–256, plus the MSIX logo PNGs listed in ARCHITECTURE §7.
- **Corners:** each is one L-shaped stroke, arm length 30 % of the glyph box, stroke 6.5 % of the tile (10 % at 32 px and below), round ends.
- **Crosshair:** 28 % of the glyph box, stroke 70 % of the corner stroke. It is left out below 24 px, so small sizes stay crisp.
- **Tile:**
  - macOS: Apple's 1024 grid (100 px margin, corner radius 22.5 % of the tile, soft shadow);
  - Windows: 4 % margin, no shadow.
- **Tray glyphs:** the same corners and crosshair with no tile, white (`onDark`) or black (`onLight`); the success variant swaps the crosshair for a checkmark.
- `tools/make-icons.py` (Pillow) generates every file from these values. The output is **committed**, and builds don't regenerate it:

  | Folder | Files |
  |---|---|
  | `shared/assets/icon/` | `AppIcon-1024.png` (master) |
  | `…/macos/` | `icon_<16–512>[@2x].png`, `AppIcon.icns` |
  | `…/windows/` | `AppIcon.ico` (16–256), MSIX logos `Square44x44Logo`, `Square150x150Logo`, `Wide310x150Logo`, `StoreLogo` at scale-100/200, and unplated `Square44x44Logo.targetsize-*` |
  | `…/windows/tray/` | `tray-viewfinder-onDark.ico`, `tray-viewfinder-onLight.ico`, `tray-check-onDark.ico`, `tray-check-onLight.ico` |

## 11. Images

| File | Source | Use |
|---|---|---|
| `shared/assets/avatar.png` | couch-lab `app/src/main/res/drawable-nodpi/avatar.webp`, converted to PNG, 320² | Author row |
| `shared/assets/sparky.jpg` | couch-lab `app/src/main/res/drawable-nodpi/sparky.jpg` | Sparky card |
