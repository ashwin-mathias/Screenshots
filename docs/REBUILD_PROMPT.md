# Rebuild prompt

Paste the prompt below into a new Claude Code session (or any coding agent) that has access to the `ashwin-mathias/Screenshots` repo. If the agent can't read the repo, attach `screenshot-rebuild-context.md` (made by `./docs/make-bundle.sh`).

> The signing certificates are **not in the repo**. They're in GitHub Actions secrets. A build signed with a different certificate won't update over the installed app (UPD-004), and on Windows it's treated as a different publisher.

---

```text
You are building "Screenshot", a small menu-bar (macOS) / system-tray (Windows) utility that captures a selected
area of one display, saves the original PNG to Documents/screenshots, and copies the image to the clipboard.
There are two native apps — Swift/AppKit for macOS 13+ (universal) and C#/.NET 10 + WPF for Windows 11 x64 — built
from one specification. The version is in the VERSION file. If this prompt and the docs disagree, the docs win.

## Read first (source of truth, in this order)
1. docs/SPEC.md          – what the app does on both platforms (requirement IDs, exact on-screen text)
2. docs/DESIGN.md        – sizes, layout, icons, overlay and preview design
3. docs/ARCHITECTURE.md  – stack, repo layout, components, shared test cases, packaging, signing, CI
4. docs/DECISIONS.md     – settled decisions and lessons (do not reopen them)
5. docs/ACCEPTANCE.md    – automated (T, K) and manual (A–G) checks, and regressions (R)
If anything is unclear, follow the spec's wording and list the question in your final report.
Don't invent new behaviour.

## Hard requirements
- No third-party runtime libraries. Windows may use only Microsoft build/test packages
  (CsWin32, MSTest, Windows SDK projection).
- Save first, then copy (CAP-006). Never overwrite (STO-004). Never claim success early.
- Both apps must pass the same JSON cases in shared/testcases/. When behaviour changes, change the case file first.
- The preview must never take keyboard focus (PRE-001).
- No keyboard hooks or Input Monitoring. Register system hotkeys only (KEY-001).
- Network only for updates (UPD-006). Never download or install without the user choosing Install (UPD-003).
  Verify the checksum AND the signing certificate (UPD-004).
- Keep the IDs, preference keys and folder exactly as written (SPEC §3, §16).
- All on-screen text exactly as in SPEC.md, including "·", "…" and "—".
- Never commit certificates, keys or passwords.

## How to work
1. Plan: list the files you'll create (ARCHITECTURE §2) and the order. Show the plan, then continue.
2. Build in vertical slices, each one compiling, tested and pushed before the next:
   a. Repo skeleton, VERSION, shared files and test cases, CI workflow with test jobs.
   b. Support libraries on both platforms (store, naming, recents, thumbnail, version, update feed) + tests T01–T17.
   c. macOS app: menu, shortcuts, permission, capture, clipboard, preview, recents, welcome, help/about, login.
   d. Windows app: tray + menu, hotkeys, screen grab + selection overlay, capture, clipboard, preview, recents,
      welcome, help/about, startup task.
   e. Updater on both platforms (check, prompt, download, verify, install) + T18–T19.
   f. Packaging and signing (build-app.sh, build-msix.ps1, AppxManifest), the release job, README install steps.
3. After each slice, re-read the matching SPEC and DESIGN sections and compare them with your code line by line
   (text, sizes, order, rules). Fix differences before moving on.
4. Builds run on GitHub Actions. If a build fails, read the ::error:: annotations, fix and push again.
5. Finally, go through docs/ACCEPTANCE.md and report each item as: verified in CI / verified in code /
   needs device test / not met (with the reason). Don't claim anything is verified unless you checked it.

## Definition of done
- CI passes on both platforms. A push that bumps VERSION publishes vX.Y.Z with the five assets (SPEC §14).
- Every SPEC requirement is implemented on both platforms or is marked platform-only in the spec.
- The final report lists deviations (should be none), questions found, and the device test checklist.
```

---

## Notes for Ashwin
- **To change the app, change the docs first** (SPEC/DESIGN), then build. The docs stay the source of truth.
- **This cloud workspace can't run macOS or Windows apps.** CI builds and tests them, but the device checks in ACCEPTANCE (A–G) need your Mac and PC.
- **Keep the docs current.** Each release updates the affected docs in the same commit as the code (CLAUDE.md).
