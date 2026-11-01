# Screenshot — documentation

| File | What it answers |
|---|---|
| [SPEC.md](SPEC.md) | What the app does on macOS and Windows: every requirement, rule and on-screen text |
| [DESIGN.md](DESIGN.md) | How it looks: fonts, colours, icon, menu, preview, overlay and window layouts |
| [ARCHITECTURE.md](ARCHITECTURE.md) | How it's built: stack, repo layout, components, shared test cases, packaging, signing, CI |
| [DECISIONS.md](DECISIONS.md) | Why: settled decisions, open questions and lessons from past bugs |
| [ACCEPTANCE.md](ACCEPTANCE.md) | How to prove it works: automated tests, device checklist and regression tests |
| [REBUILD_PROMPT.md](REBUILD_PROMPT.md) | The prompt for building the app from these docs |

- Every release updates the affected docs **in the same commit** as the code.
- `./docs/make-bundle.sh` joins the five spec files into one `screenshot-rebuild-context.md` for pasting into a chat.
