# Dotted UI verification

2026-09-26: Unity 6000.5.5f1 graphics-enabled Play Mode fixture `Tagtag.Tests.PaperVisualTests`: 5/5 passed. After correcting capsule corner sizing, `CameraInventoryFlowUsesFullScreenAndExplicitPlacement`: 1/1 passed.

Commands used the sticker-creation worktree with `-batchmode -buildTarget StandaloneOSX -runTests -testPlatform PlayMode -testFilter <fixture>`, writing results to `/tmp/tagtag-diecut-play.xml` and `/tmp/tagtag-diecut-confirm.xml`.

Inspected Home, camera placement, Explore teaser, My Stickers, and compact enlarged-text inventory captures. These are rendered Unity fixtures with simulated camera imagery, not physical-device screenshots. Native maps and real camera lighting were not verified in this pass.

The camera art uses a transparent generated PNG and a separate live label. Container outlines use wider-spaced dots; controls use tighter rounded outlines. Home and Explore actions remain plain inside dotted containers to avoid nested decoration.
