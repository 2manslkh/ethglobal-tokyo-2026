# Publishing dogfood verification

Source: be3cf0a (combined publishing and navigation). Unity 6000.5.5f1.

- Edit Mode: 120/120 passed.
- Graphics Play Mode: 8/8 passed.
- Built macOS player: 8/8 passed. Build completed 2026-09-26T00:38:07.539000+00:00; suite started 2026-09-26T00:38:10+00:00, ended 2026-09-26 00:38:52Z.

The local player callback is fresh relative to this build. The editor return connection hung; the completed owned test processes were stopped only after reading the passing result. Captures come from the built player with simulated camera imagery and fixture data. No physical GPS, camera, native keyboard, or publication latency result is implied.

Separate ARKit preparation and final iOS export succeeded. Both unsigned and development-signed Xcode builds passed. The new Core Location bridge is in the Sources phase and linked binary. Exported and built plist metadata enable visible dark native status text. After the owner connected Dawg (iPhone 15 Pro Max, iOS 26.6.1), USB installation completed successfully without uninstalling the existing app. Real publishing latency and device interaction checks remain pending the owner’s retest.
