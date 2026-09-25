# tagtag UI fonts

Instrument Sans was copied from `sticker-app/unity/Assets/Resources/Fonts/` on 2026-09-26. It remains the body, field, button, and navigation-label family. The native map uses `InstrumentSemibold.ttf`. The player has no runtime dependency on the source checkout.

- Instrument Sans Regular and SemiBold: `@expo-google-fonts/instrument-sans` 0.4.2, Google Fonts `ofl/instrumentsans`.
- Shadows Into Light Regular: [Google Fonts source](https://github.com/google/fonts/blob/main/ofl/shadowsintolight/ShadowsIntoLight.ttf) and [OFL notice](https://github.com/google/fonts/blob/main/ofl/shadowsintolight/OFL.txt), downloaded 2026-09-26. It is used only for display and title text. TTF SHA-256: `1347863151acdc00fa281daaba1a3543dbce5870b55f9cf7479a15bb84007681`.

Both families are redistributed under the SIL Open Font License 1.1. Their notices are `Instrument-OFL.txt` and `ShadowsIntoLight-OFL.txt` in this folder. Unity's UI Toolkit dynamic OS fallback supplies glyphs absent from the Latin-only shipped faces, including Japanese. Verify script fallback and system text sizing on device.
