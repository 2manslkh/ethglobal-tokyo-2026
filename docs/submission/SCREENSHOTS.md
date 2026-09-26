# Screenshot set

These are copied, unedited evidence images from the repo. They are ready to review and upload individually. The filenames and captions keep simulator/native test captures separate from automated UI fixtures. They do not demonstrate a completed two-phone AR collection.

| Order | File | Capture source | Suggested caption |
| --- | --- | --- | --- |
| 1 | [Login](screenshots/01-login-simulator.png) | iPhone simulator capture from `docs/verification/login-refresh/iphone.png` | Start with Apple or Google sign-in and Taggi's animated welcome. |
| 2 | [Sticker book](screenshots/02-sticker-book-simulated.png) | Unity automated visual fixture from `docs/verification/ar-sticker-flow/home-populated.png` | Discovered stickers fill a personal, paginated book. |
| 3 | [Sticker designs](screenshots/03-taggi-stickers-simulated.png) | Unity automated visual fixture from `docs/verification/twelve-taggi/inventory.png` | Choose from twelve Taggi poses or add a design. |
| 4 | [AR scan UI](screenshots/04-ar-scan-ui-simulated.png) | Unity automated visual fixture from `docs/verification/stick-note/camera-scan-ready.png`; background is simulated | Scan a surface and capture a sticker placement. |
| 5 | [Confirm spot](screenshots/05-confirm-spot-simulator.png) | Native MapKit picker in iPhone simulator from `docs/verification/location-confirmation/compact.png` | Confirm a sticker's map pin when GPS is approximate. |
| 6 | [Collection celebration](screenshots/06-found-celebration-simulated.png) | Unity automated visual fixture from `docs/verification/celebration/found.png` | A found sticker becomes a moment in your book. |

## Better final captures

For the final showcase, replace the simulated scan and collection screens with an actual iPhone recording/capture once the two-phone flow passes: creator places and publishes → finder opens Explore → finder recovers and taps the sticker → private note and book entry. Capture the app screen directly with iOS screen recording or a device screenshot, without personal account or precise location details visible. Retain the original PNGs and note the device/build in [DEVICE_VERIFICATION.md](../DEVICE_VERIFICATION.md). The current screenshots are best used as interface previews, with their origin stated where captions are supported.
