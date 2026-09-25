# Paper UI source provenance

The UI components in this directory were adapted on 2026-09-26 from the user's local `~/Desktop/coding-projects/sticker-app/unity` checkout. The hashes identify the reference files as read. The shipped app uses its own copies and has no runtime dependency on that checkout.

| Reference under source `unity/Assets/` | SHA-256 | tagtag adaptation |
| --- | --- | --- |
| `StickerHunt/Playable/PrimitiveControls.cs` | `3961a61e7052c9ada15e8e915b0029bba67ae89b5eab5b64db19e2a621e837b2` | `PaperPrimitives.cs`: button activation, selection marker, switch, and supported field text |
| `StickerHunt/Playable/ScreenShell.cs` | `ae7cf899df4635cf6064bd1bd73a85979ac240aa1832fc403951c56fb3b8a386` | `TagtagAppView.cs`: persistent shell, safe insets, hidden-scrollbar scrollers, overlay host |
| `StickerHunt/Playable/BottomSheet.cs` | `b7fbf64c99d65c6d81af8077c452cece785e65fdcdda05ae766f3df99aab9422` | `PaperSheet.cs`: drag, scrolling, dismissal, focus loop and return |
| `StickerHunt/Playable/ConfirmationSheet.cs` | `064721e0ccbb0f1f76a626b720a3b76cab25164bd81efd28a7babe962a059c40` | Account withdrawal, block and delete confirmation content in `TagtagAppView.Account.cs` |
| `StickerHunt/Playable/AppNotice.cs` | `a9ba1050dcbd32cbfab5757fd81309394ed08718f425868d0e456ddd0aa5949f` | Shared notice treatment in `TagtagAppView.cs` and `Paper.uss` |
| `StickerHunt/Playable/BottomNav.cs` | `56512ddbc14190afd9bfc8b655a57a25cc068bdb9a121b4062b622940b47b346` | Home/STICK/Explore icon and label navigation in `TagtagAppView.cs` |
| `StickerHunt/Playable/AppNavigationMotion.cs` | `3a4a338dc80285a779dfab9b3dda034adb36a187b31318b1667ef451baf80301` | `PaperNavigationMotion.cs`: destination transitions and continuity helper |
| `StickerHunt/Playable/AppMotion.cs` | `89f3b9454c1a8fdc8a76c8311b650d3e8ae79e0a1ac1257c0c0cb9e1fc70c6d4` | `PaperMotion.cs`: source timing, easing, spring, cancellation and reduced motion |
| `StickerHunt/Playable/AppTypography.cs` | `832abd157d5f5a5ef14c1010f5e3ced84eaf8f8d24dbcb82a757f500977e6aed` | `TagtagAppView.cs`: role fonts and text scaling |
| `StickerHunt/Playable/LineIcon.cs` | `f354d748c8d80e2761e410d76a06be4dae930d9deb0c5aeebbccd43dd4a0564a` | `PaperIcon.cs`: consistent vector icons |
| `Resources/Playable.uss` | `36cfb85ef5c4ee11ae8220794fc64c720798b5c4380ae37dec438da4ad1484b7` | `Resources/Tagtag/UI/Paper.uss`: state styles mapped to tagtag paper, ink and yellow |
| `StickerHunt/Core/SurfaceTap.cs` | `339031229b2cfa4f2cb0ed8abdc952ee7e7c8d03e5ae8398f7e41e088ca82638` | `PaperCameraInput.cs`: placement tap cancellation on drag, long press, and multiple contacts |
| `StickerHunt/Core/SurfaceGesture.cs` | `2a988237578596ad90ff8e0a30f543c93d85eae51e6db72e3c965fd92d5aaa8d` | `PaperCameraInput.cs`: owned surface contact tracking and centroid for drag, pinch, and twist |
| `StickerHunt/Core/PlaySession.cs` | `de18f4abe91e7c613cb7308506d046bbbd64591431cf798f704bc7939748d0d4` | `PaperCameraInput.cs`: width clamping and rotation wrapping from `PlacementGestures` |
| `StickerHunt/Playable/DeviceApp.cs` | `0f44e2a510fed2558263fc8f6cf37fb5dcb337a2f08445ca39189e5e9081031f` | `TagtagAppView.Camera.cs` and `TagtagAppView.Screens.cs`: fullscreen camera surface, inventory, selection, placement guidance, and adjustment controls; tagtag retains its own publish service |

The source checkout did not contain a root code license notice. The font files and their SIL Open Font License notices are documented in `Assets/Resources/Tagtag/Fonts/README.md`.

The later device UI pass uses the project-generated Taggi artwork at `Resources/Tagtag/Navigation/{home,stick,explore}.png` for tabs and the official Google Fonts Shadows Into Light face for display/title text. Its source, checksum, and license are recorded in the font README. Instrument Sans remains the readable body/control font.
