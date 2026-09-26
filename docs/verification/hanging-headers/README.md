# Hanging headers

Unity Editor 6000.5.5f1, graphics-enabled Play Mode, 2026-09-26.
Captures use a real UI Toolkit panel rendered at 390×844 and 320×568.
These are Editor fixtures, not iPhone or simulator screenshots. Native MapKit and
live AR imagery are unavailable in these fixtures.

- [Home, scrolled](hanging-home.png): title remains fixed above the design gallery.
- [Explore](hanging-explore.png): stationary Recenter outside the moving paper.
- [Camera](hanging-camera.png): Close remains separate and usable.
- Compact large titles: [Home](hanging-compact-Home.png),
  [Explore](hanging-compact-Explore.png), [Camera](hanging-compact-Stick.png).
- [Entrance recording](entrance.gif): rendered frames at approximately 20 fps,
  showing the two fixed anchors and knots following the settling paper.

The compact fixture applies 48-point header text directly. Reduced motion is
exercised via the existing `reduced-motion` root class; this change does not add
or restore account preferences removed by the concurrent settings task.

## Automated checks

Baseline: all three new hanging-header tests failed because the header was absent.
After implementation: all 16 HomeLibraryVisualTests and all 217 Edit Mode tests
passed. Checks cover fixed headers during scrolling, refresh suppression,
compact large titles, reduced-motion settling, page cleanup, and rapid navigation.

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -projectPath "$PWD" -buildTarget StandaloneOSX \
  -runTests -testPlatform PlayMode -testFilter Tagtag.Tests.HomeLibraryVisualTests \
  -testResults /tmp/tagtag-hanging-green.xml -logFile /tmp/tagtag-hanging-green.log
"$UNITY" -batchmode -nographics -projectPath "$PWD" -buildTarget StandaloneOSX \
  -runTests -testPlatform EditMode -testResults /tmp/tagtag-hanging-edit.xml \
  -logFile /tmp/tagtag-hanging-edit.log
```


The broader `PaperVisualTests;CelebrationVisualTests` Play Mode run passed 14/18.
The same four failures were present in the concurrent settings task's earlier
run (`/tmp/tagtag-settings-play.xml`), before this verification:

- `CompactRewardsIgnoreLegacyTextSizeAndKeepActionVisible`: exact scale equality.
- `InterruptionSettlesMotionWithoutDismissingOrReplaying`: exact scale equality.
- `CompactLargeTextCreatorTilesKeepLabelsAndArtworkInsideTheirBounds`: expected
  19-point text, actual 14, following the separately owned preference removal.
- `LoginSupportsRetryAndIgnoresLegacyReadingPreferences`: expected button height
  at least 52, actual 51.1875.

Results: `/tmp/tagtag-hanging-ui.xml`; log: `/tmp/tagtag-hanging-ui.log`. Those
separately owned settings changes are not part of the header commit. The full
UI suite is not claimed green.

## Device follow-up

On a physical iPhone, check strings extend behind the native status bar to the
screen edge while title text and controls stay inside the safe area. Check live
camera contrast, Recenter with MapKit active, sheet/celebration layering, and
smooth motion when quickly switching pages. Physical-device verification and an
iOS export were not performed for this change.

## Review assignment

Coordinator owns implementation, integration, Unity builds, and Git writes.
Independent read-only review: GPT-6-Sol, medium thinking, Herdr tab
“Hanging headers”, agent `hanging-header-review`. Reviewer owns no files.

Review completed with no actionable findings.
