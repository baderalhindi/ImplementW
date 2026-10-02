# Code Quality
- No generic "AI slop" — be direct, truthful, and precise
- Follow KISS, DRY, and SOLID principles
- Use canonical, conventional naming (no cutesy or ambiguous names)
- Fill every PR description from `.github/PULL_REQUEST_TEMPLATE.md`: put `TASK-nnn` in the title and under `## Task`, list the commands actually run and their real results under `## Test evidence` (write "n/a — no backend or frontend code changed" for dotnet/npm on docs or infra-only PRs), and tick every box under `## Security checklist`.
- Tick a checklist box only after checking the diff for it. If one can't honestly be ticked, leave it unticked and say why; never tick it just to make the PR look complete.
- Name branches `type/task-nnn-short-description`, matching the workbook's Branch column.
- After pushing, set the body with `gh pr edit <n> --body-file <file>` and confirm the CI checks with `gh pr checks <n>`.

# Workspace Location
- All work stays inside `/Volumes/SanDisk/Bader`. The application lives at `/Volumes/SanDisk/Bader/Development/Projects/AHDA/ImplementW`; never create project, cache, temp or backup folders outside the Bader folder (not at the SanDisk root, not in `/tmp`, not in the session scratchpad).
- Package caches live in `/Volumes/SanDisk/Bader/Development/Caches` (`nuget`, `npm`, `pip`, `bun`). If `NUGET_PACKAGES`, `npm_config_cache`, `PIP_CACHE_DIR` or `BUN_INSTALL_CACHE_DIR` point outside the Bader folder, override them to the Bader paths before running a command.
