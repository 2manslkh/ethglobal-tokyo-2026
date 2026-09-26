# Home paper tabs

Verified 2026-09-26 with Unity 6000.5.5f1, graphics-enabled Play Mode fixtures.
All 16 HomeLibraryVisualTests passed (0 failed). Reviewed 390 × 844 and
320 × 568 captures: labels stay on one line, the tab row fits within the page,
and the selected section retains its warm paper highlight.

```sh
/Applications/Unity/Hub/Editor/6000.5.5f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath "$PWD" -buildTarget StandaloneOSX \
  -runTests -testPlatform PlayMode -testFilter Tagtag.Tests.HomeLibraryVisualTests \
  -testResults /tmp/tagtag-home-tabs-tests.xml -logFile /tmp/tagtag-home-tabs-tests.log
```

- [Collected](home-collected-empty.png)
- [Compact Home](home-compact-empty.png)
- [Your Designs](home-my-designs.png)

These are Unity render-texture captures with fixture data, not iPhone screenshots.
The existing suite exercises section switching and scroll retention. The new
280 ms press wobble uses PaperMotion cancellation and reduced-motion handling;
its tactile feel and touch cancellation still require a physical-device check.
Tap each tab, tap an already-selected tab, tap rapidly, and start a scroll from
a tab. Confirm immediate switching on taps, a slight settling wiggle, and no
stuck rotation after leaving Home or backgrounding.
