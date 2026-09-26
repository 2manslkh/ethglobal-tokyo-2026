# STICK capture and note

The circular STICK control captures the spot after Scan ready, then opens the
single-field note sheet. These screenshots use simulated camera imagery.

- [Ready to capture](camera-scan-ready.png)
- [Your Note](camera-note-after-placement.png)
- [Compact Your Note](note-compact-fixed-defaults.png)

Verified in the isolated feature checkout: 247 Edit Mode tests and 12 graphics
PaperVisualTests passed. Initial capture regressions failed before implementation;
the stale-error reopen regression failed before its fix. Independent review found
no remaining issues in the reviewed fixes.

Commands: Unity `-buildTarget StandaloneOSX -runTests -testPlatform EditMode`,
and graphics-enabled `-buildTarget StandaloneOSX -runTests -testPlatform PlayMode
-testFilter Tagtag.Tests.PaperVisualTests`. Results:
`/tmp/tagtag-stick-note-final-edit.xml`, `/tmp/tagtag-stick-note-final-play.xml`.

The combined iOS-target checkout passed 253 Edit Mode tests and was signed,
installed over the existing app on Dawg. (iPhone 15 Pro Max), and launched.
Physical AR acceptance remains pending.

See [device verification](../../DEVICE_VERIFICATION.md) for combined build,
installation and pending physical AR acceptance steps.
