# Shared test cases

Both apps load these files in their test suites (ARCHITECTURE §5). A rule lives here once; if the two apps ever disagree, one of their test runs fails.

- Change a case **first**, then make both apps pass it.
- Dates without a zone are **local time**. Dates ending in `Z` are UTC.
- `null` in an expected value means "no result" (see each file's `notes`).
