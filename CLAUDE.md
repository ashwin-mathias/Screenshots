# Working on Screenshotz

Read `docs/README.md` first. The docs in `docs/` are the source of truth for behaviour, design and architecture, on **both** platforms.

## Every release (required, same commit as the code)
1. Bump `VERSION` (one version for macOS and Windows).
2. Add a newest-first entry to `shared/changelog.json` (`version`, `date` YYYY-MM-DD, `title`, `notes`). Its version must equal `VERSION`, or CI fails.
3. Update the docs the change touches:
   - `docs/SPEC.md` for behaviour, shortcuts or on-screen text. Keep the "Version described" line current.
   - `docs/DESIGN.md` for sizes, layout, icons or colours.
   - `docs/ARCHITECTURE.md` for libraries, components, stored keys, packaging or CI.
   - `docs/DECISIONS.md` for any decision Ashwin made (with the reason) and any lesson from a bug.
   - `docs/ACCEPTANCE.md`: add tests for new features, and a regression row (R#) for every bug fixed.
   - `shared/testcases/` when a shared rule changes. Change the case first, then both apps.
4. Update README if the shortcuts or install steps changed.
5. Commit, push, and watch both GitHub Actions jobs. If one fails, read the `::error::` annotations.

## Rules
- **Parity:** every behaviour change goes into both apps in the same release, or is written into SPEC as a platform difference.
- Never commit certificates, keys or passwords (`.p12`, `.pfx`, `.cer`). They live in GitHub Actions secrets.
- This cloud workspace can't run macOS or Windows apps. Rely on CI for builds and tests, and list device checks for Ashwin.

## Working style with Ashwin
- Discuss and agree before building when he asks to; wait for "build it".
- Reply in Minto style: bold key points first, then supporting bullets.
- Printable documents: black text, minimal, clean (Scandinavian / Japanese style).
