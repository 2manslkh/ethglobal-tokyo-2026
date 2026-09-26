# Branch consolidation — 2026-09-27

All named local and origin branches, plus detached worktree commits, were included in `main` at integration commit `70b73c3`.

## Integration

- Merged remote main and the submission pitch materials.
- Merged the Tokyo NFT rollout, recoverable phone wallet, profile Etherscan link, and live mint verification notes through `5291141`.
- Consolidated `feat/stick-capture-note`. Its implementation was already integrated as `e850185`; retained the later title field, wallet UI, and tap-to-collect behavior.
- Consolidated `fix/approximate-ar-recovery`. Its implementation was already integrated; retained the later measured-uncertainty collection policy and added its historical rollout record.
- Consolidated both publication-quota branches. Retained the 100-publication limit and added the complete boundary test: 100 preparations, idempotent retry, rejection of 101, and a separate user's allowance.

## Verification

- Unity Edit Mode: 259 passed, zero failures. `/tmp/tagtag-consolidation-edit.xml`.
- Unity Play Mode `Tagtag.Tests.PaperVisualTests`: 12 passed, zero failures. `/tmp/tagtag-consolidation-play.xml`.
- `cd backend && npm test`: 86 passed, one emulator test skipped, zero failures.
- `python3 scripts/nft-staging/test_hosting.py`: three passed.
- `git diff --check`: passed.
- No new device build, installation, or backend deployment was performed as part of this consolidation.

## Preserved work

Existing worktrees and their uncommitted files were retained. The root's `.gitignore`, `TODO.md`, and untracked `ProjectSettings/SceneTemplateSettings.json` were not included in the integration commits. Uncommitted changes in the AR device, sticker final, NFT staging, and capture-note worktrees remain there for their owners to finish.

Only fully merged branches without an attached worktree were selected for deletion. Branches still attached to worktrees remain available; their committed history is already included in main.
