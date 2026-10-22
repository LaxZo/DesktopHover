# DesktopHover workflow

The user requested on 2026-10-22: use Graphify and record subsequent work in Obsidian.

- Apply the installed `graphify` skill when investigating this project. Use the existing `graphify-out/graph.json` for focused queries, refresh it after meaningful source changes, and verify graph findings against the actual source before editing. Keep extraction local and code-only unless the task needs broader document analysis.
- Apply the installed `obsidian` skill. After each substantive task, update the project note and append a dated entry to its work journal with the request, decisions, changes, verification results, and unresolved work. Record accurate summaries, not raw transcripts or secrets. This is part of active project work; no background synchronization is configured.
- Obsidian vault: `C:/Users/nauri/Documents/Codex/2026-10-22/https-github-com-graphify-labs-graphify/outputs/Obsidian`.
- Project note: `Projekti/DesktopHover.md`. Journal: `Projekti/DesktopHover - Darba zurnals.md`. Preserve existing content and make sure new wiki links resolve.
- Use the local Windows Graphify installation described in its skill; do not reinstall it just because it is absent from PATH. Report extraction limitations honestly.
- Preserve the user's uncommitted changes. Build the application with `build.ps1`; temporary verification files should not enter the source graph.
