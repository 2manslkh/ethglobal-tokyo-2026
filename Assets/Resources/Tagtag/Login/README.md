# Login assets

- `Background.mp4`: supplied `~/Desktop/loading-screen-sticker.mp4` (H.264, 720×1280, approximately 6 seconds). Remuxed without audio or the attached preview image; video frames are unchanged.
- `Poster.jpg`: a frame at one second from that clip, used before playback and for reduced motion or decoder failure.
- `Apple.png`: unchanged white Apple mark extracted from Apple's official Continue with Apple image at https://appleid.cdn-apple.com/appleid/button?color=black&border=false&width=300&height=44&type=continue&scale=3 . Black background was removed to retain antialiased white edges. Used only for Apple authentication.
- `Google.png`: unchanged 20-point multicolor G from the 4× iOS light square button in https://developers.google.com/static/identity/images/signin-assets.zip (downloaded 2026-09-26). Used only for Google authentication.

Button standards: [Apple](https://developer.apple.com/design/human-interface-guidelines/sign-in-with-apple/) and [Google](https://developers.google.com/identity/branding-guidelines). Google uses Google Sans Medium from Google Fonts, with its OFL notice under `../Fonts/`. Apple uses the operating system Helvetica Neue/Helvetica/Arial font stack.
