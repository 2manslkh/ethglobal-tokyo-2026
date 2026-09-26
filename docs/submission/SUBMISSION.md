# tagtag — ETHGlobal Tokyo 2026 submission

Copy for the [ETHGlobal project form](https://ethglobal.com/events/tokyo2026/project). Prepared 2026-09-26 JST from the repository and the signed-in form. The **tagtag** project draft was created in ETHGlobal with the name, Gaming category, and 📍 emoji. The project has **not** been submitted for judging; the remaining fields and uploads are still incomplete.

## Create project

| Form field | Answer |
| --- | --- |
| Project name | **tagtag** |
| Category | **Gaming** (available in the category picker) |
| Emoji | 📍 |

The created draft exposes the steps **Project details → Images → Tech stack → Select prizes → Video → Future → Final**. The sections below follow those form steps.

## Short description

Leave AR stickers at real places, discover their notes, and collect the moments.

80 characters, under the form's 100-character maximum.

## Project description

**Find your places, collect your moments.** tagtag turns a walk through a city into a shared sticker book. On iPhone, you choose a Taggi sticker or make your own, attach it to a real surface in AR, capture the spot, and leave a note. Other explorers see a nearby map pin and a teaser, visit the place, recover the saved AR placement, and tap the sticker to add a copy to their book. The original stays available for the next person. The full note is revealed only after collection.

The experience is designed around small, personal discoveries rather than a leaderboard. A paper-white interface, hand-drawn Taggi art, and a paginated collection make each find feel like a keepsake. Apple or Google sign-in restores a person's designs and collections. The app also includes reporting, blocking, and publication withdrawal.

The iPhone app, API, and private AR-map storage are implemented. One physical iPhone publication through the approximate-GPS map confirmation flow is recorded. Two-phone AR recovery and collection still need device verification. An optional Ethereum Sepolia ERC-721 souvenir path exists in code but is **disabled** pending contract, worker, wallet, and live device checks; do not present a mint as part of the working demo. [Readiness evidence](../FEATURE_READINESS.md) and [device results](../DEVICE_VERIFICATION.md) explain the distinction.

## How it's made

The client uses Unity 6000.5.5f1 and UI Toolkit. AR Foundation and ARKit track surfaces, save the creator's world map, and recover the placement on a finder’s iPhone. Native MapKit shows nearby sticker pins and lets a creator confirm a spot when GPS is approximate. Native iOS bridges handle Apple/Google sign-in, camera permissions, image import and editing, and optional Image Playground creation.

Firebase Authentication identifies users. A Node.js API on Google Cloud Run authorizes publishing, nearby discovery, collection, and private-note access. Firestore stores sticker and collection records; private Cloud Storage holds AR maps and the original-spot reference photo. The API returns public teasers for browsing, while the note stays behind the collection flow. Nearby location and AR-map recovery are gameplay gates; they are not cryptographic proof of presence.

The repository also contains a Solidity ERC-721 souvenir contract, Thirdweb embedded-wallet integration, and a backend mint queue for Ethereum Sepolia. These are **implemented but not live** in the submitted app configuration. Collection works without a blockchain transaction, and no NFT mint or transfer should be claimed until the [staging checklist](../NFT_STAGING.md) is complete.

## Links and uploads

| Item | Answer / status |
| --- | --- |
| Source code | https://github.com/2manslkh/ethglobal-tokyo-2026 — verify public visibility and push the final commit before submitting. |
| Demonstration link | Use the existing [TestFlight invitation](https://testflight.apple.com/join/cUnAbdSY), per the owner's choice. The recorded external build is **Waiting for Review** and predates the latest location and AR changes; keep that limitation visible in the description. |
| Demo video | To be recorded. The Video step calls it optional, but the project-creation guidelines say it is required if applying for finalist prizes. See [DEMO_VIDEO.md](DEMO_VIDEO.md). |
| Logo | [logo.png](logo.png), the actual 512×512 app icon. |
| Cover image / project banner | [cover.png](cover.png), a 1672×941 promotional illustration generated for this submission with the app icon as a Taggi reference. The form recommends 16:9. |
| Screenshots | Six prepared PNGs in [screenshots/](screenshots/) with provenance in [SCREENSHOTS.md](SCREENSHOTS.md). The form requires **at least three**. Upload only images that accurately match the demonstrated build. |
| Figma | Add a public design link if desired and available; no verified URL was found in the current repo. |

## Form fields still to complete

**Project details:** The form asks for a demonstration link, a short description (maximum 100 characters), description (minimum 280 characters), how it's made (minimum 280 characters), and at least one public GitHub repository selected through the connected GitHub account picker. The copy above meets the text limits. The TestFlight link is not yet a working public demo for the latest build.

**Tech stack:** Select only values actually offered by each picker. Suggested answers from the source tree:

| Question | Draft answer |
| --- | --- |
| Ethereum developer tools | Thirdweb embedded wallets and `viem` are present for the disabled Sepolia souvenir path; mark as staging/implemented, not a live integration. |
| Blockchain networks | Ethereum Sepolia is the target of the disabled NFT path. The current app's core discovery journey does not interact with a chain. |
| Programming languages | C#, JavaScript, Swift, Objective-C++, Solidity. |
| Web frameworks | None for the iPhone client; the Node.js API uses built-in HTTP rather than a frontend framework. |
| Databases | Firestore. |
| Design tools | Confirm whether Figma was used before selecting it; no current share URL is documented. |
| Other technologies | Unity, AR Foundation, ARKit, MapKit, Firebase Authentication, Cloud Storage, Google Cloud Run, Sharp. |
| AI use (free text) | “Codex assisted with C#, native iOS, backend, contract, tests, documentation, and visual review under human direction. OpenAI imagegen created Taggi poses 5–12 from the supplied mascot reference; prompts and provenance are in `docs/mascot/TAGGI_PRESETS.md`. The original four poses and login video were supplied. The submission cover was generated with imagegen from the app icon. Source history, plans, and verification records are public in the repo.” |

**Future:** Yes — interested in continuing tagtag through grants or accelerators.

## Partner prizes and judging

The Select prizes step asks for **Building from Scratch** or **Continuity Track**, then **Top 10 Finalist & Partner Prizes** or **Partner Prizes only**. The owner confirmed **Building from Scratch**: all project work began during the event. Choose **Top 10 Finalist & Partner Prizes** to enter main judging, and leave **all partner prizes unselected**. The form permits up to three partners and does not state a minimum. The finalist option was selected in the browser with every partner box unchecked, but the step cannot be saved until earlier required project details, images, and tech stack are complete.

**Partner prize answer: none.** Leave the partner checkboxes and the optional “other partners' technologies” picker empty. The disabled Sepolia/Thirdweb path does not establish a live partner integration. A 2–4 minute demo video is required for the selected finalist stream.

## Track and provenance

The repository's first commit is `0593383` at **2026-09-25 21:19 JST**. Check the official event start time before choosing **From Scratch**; if any project-specific code, designs, or assets predate the event, choose the appropriate continuity track and describe them. The first four Taggi poses and login video were supplied; the eight additional Taggi poses were generated during the event. The project history contains small feature commits and the design, planning, and verification records.

AI tools assisted implementation, documentation, and visual review. The eight new Taggi preset images were generated with OpenAI imagegen using the supplied mascot as reference; prompts and process are in [TAGGI_PRESETS.md](../mascot/TAGGI_PRESETS.md). UI and AR source provenance is recorded in [UI SOURCE_PROVENANCE.md](../../Assets/Tagtag/UI/SOURCE_PROVENANCE.md) and [AR SOURCE_PROVENANCE.md](../../Assets/Tagtag/AR/SOURCE_PROVENANCE.md). The repository's `docs/plans/`, `docs/verification/`, and commit history document human direction, testing, and iteration. Disclose the AI-assisted areas in the final form as requested by [ETHGlobal's rules](https://ethglobal.com/events/tokyo2026/info/details).

## Submission check

- [x] Confirm the track: Building from Scratch; disclose supplied Taggi assets and video.
- [x] Create the ETHGlobal project draft and inspect all steps.
- [ ] Complete required project details, tech stack, and images, then save **Top 10 Finalist & Partner Prizes** with no partner prizes selected.
- [ ] Verify GitHub is public and final commits are pushed.
- [ ] Record and upload a 2–4 minute demo at 720p or higher, with a human voice and no sped-up footage if seeking finalist prizes.
- [ ] Replace simulated UI captures with real iPhone captures for claims demonstrated on hardware.
- [ ] Confirm a working public demo link or provide clear build/run steps.
- [x] Decide partner prizes: none for this submission.
- [ ] Review the Final step's event-rule attestation, then submit by **2026-09-27 09:00 JST** and check the dashboard for confirmation.

The deadline, video guidance, track rules, AI disclosure, and prize limit come from [ETHGlobal's Tokyo 2026 submission guide](https://ethglobal.com/events/tokyo2026/info/details). The create-project sidebar still displays a generic “start from scratch” rule, while the event guide describes continuity tracks; use the event-specific track rules when making that selection.
