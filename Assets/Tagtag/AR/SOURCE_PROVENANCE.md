# AR sticker source provenance

Source repository: `~/Desktop/coding-projects/sticker-app`, commit `ec7765379ae52f292ec80d4a29983828f21c2cb2`.

| Source | Adaptation |
| --- | --- |
| `unity/Assets/Resources/DeviceSticker.shader` and `StickerSurface.cginc` | `Assets/Resources/Tagtag/AR/`: the source sticker shader and surface calculation; shader name changed to `Tagtag/DeviceSticker` |
| `unity/Assets/Resources/DeviceSticker.mat` | `Assets/Resources/Tagtag/AR/DeviceSticker.mat`: referenced by the AR visual and plane outline so the shader ships in player builds |
| `unity/Assets/StickerHunt/Device/DeviceARSession.Placement.cs` | `ArExperience.cs`: tracked, unsubsumed plane outlines, frustum availability and 10 Hz boundary updates |
| `unity/Assets/StickerHunt/Device/DeviceARSession.cs` (`Place`, `Adjust`, `Hit`) | `ArExperience.cs` and `PlacementFlow.cs`: explicit screen-point polygon raycast, same-plane drag, scale and rotation; ARKit world-map publication and recovery remain tagtag-specific |
| `unity/Assets/StickerHunt/Core/SurfaceTap.cs` and `docs/STICKER_PLACEMENT_UX.md` | Camera input contract: UI recognizes short taps and gestures; AR accepts camera points and checks the hit area, tracking and current plane |

No source glass, page curl, cloud anchor or persistence code is imported.
