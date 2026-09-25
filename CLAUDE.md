# Code Quality
- No generic "AI slop" — be direct, truthful, and precise
- Follow KISS, DRY, and SOLID principles
- Use canonical, conventional naming (no cutesy or ambiguous names)
- Fill every PR description from `.github/PULL_REQUEST_TEMPLATE.md` so the `pr-policy` check passes: put `TASK-nnn` in the title and under `## Task`, list the commands actually run and their real results under `## Test evidence` (write "n/a — no backend or frontend code changed" for dotnet/npm on docs or infra-only PRs), and tick every box under `## Security checklist`.
- Tick a checklist box only after checking the diff for it. If one can't honestly be ticked, leave it unticked and say why; never tick it just to turn the check green.
- Name branches `type/task-nnn-short-description`, matching the workbook's Branch column.
- After pushing, set the body with `gh pr edit <n> --body-file <file>` and confirm with `gh pr checks <n>`.
