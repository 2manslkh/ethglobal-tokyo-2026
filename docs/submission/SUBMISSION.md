# tagtag — ETHGlobal Tokyo 2026 submission

Copy for the [ETHGlobal project form](https://ethglobal.com/events/tokyo2026/project). Prepared 2026-09-26 JST from the repository and the signed-in form. This is a draft: no project was created or submitted in the browser.

## Create project

| Form field | Answer |
| --- | --- |
| Project name | **tagtag** |
| Category | **Gaming** (available in the category picker) |
| Emoji | 📍 |

The signed-in form currently shows these three required fields before **Create project**. The remaining form fields are gated behind that action. The sections below prepare the content described in [ETHGlobal's submission guide](https://ethglobal.com/events/tokyo2026/info/details) and seen on [ETHGlobal showcase projects](https://ethglobal.com/showcase/project-name-vndcz); check their exact labels and limits after creating the project.

## Short description

Leave AR stickers at real places, discover their notes, and collect the moments.

80 characters, suitable for a 60–100 character one-liner.

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
| Live demo | [TestFlight invitation](https://testflight.apple.com/join/cUnAbdSY) exists, but the recorded external build is **Waiting for Review** and cannot yet be joined through the public link. It also predates the latest location and AR changes. Use only after approval and a fresh build. |
| Demo video | To be recorded. ETHGlobal calls it optional but strongly encourages a 2–4 minute video; see [DEMO_VIDEO.md](DEMO_VIDEO.md). |
| Screenshots | Six prepared PNGs in [screenshots/](screenshots/) with provenance in [SCREENSHOTS.md](SCREENSHOTS.md). Upload only the images that accurately match the demonstrated build. |
| Figma | Add a public design link if desired and available; no verified URL was found in the current repo. |

## Partner prizes

**No partner prize selected in this draft.** The official form allows up to three, and each requires a truthful explanation of a working partner integration. The disabled Sepolia/Thirdweb path alone does not establish eligibility for any Tokyo 2026 partner prize. Review the [event prize list](https://ethglobal.com/events/tokyo2026/prizes) against a live demo before selecting a prize.

## Track and provenance

The repository's first commit is `0593383` at **2026-09-25 21:19 JST**. Check the official event start time before choosing **From Scratch**; if any project-specific code, designs, or assets predate the event, choose the appropriate continuity track and describe them. The first four Taggi poses and login video were supplied; the eight additional Taggi poses were generated during the event. The project history contains small feature commits and the design, planning, and verification records.

AI tools assisted implementation, documentation, and visual review. The eight new Taggi preset images were generated with OpenAI imagegen using the supplied mascot as reference; prompts and process are in [TAGGI_PRESETS.md](../mascot/TAGGI_PRESETS.md). UI and AR source provenance is recorded in [UI SOURCE_PROVENANCE.md](../../Assets/Tagtag/UI/SOURCE_PROVENANCE.md) and [AR SOURCE_PROVENANCE.md](../../Assets/Tagtag/AR/SOURCE_PROVENANCE.md). The repository's `docs/plans/`, `docs/verification/`, and commit history document human direction, testing, and iteration. Disclose the AI-assisted areas in the final form as requested by [ETHGlobal's rules](https://ethglobal.com/events/tokyo2026/info/details).

## Submission check

- [ ] Confirm the chosen track and disclose supplied/pre-existing assets.
- [ ] Create the ETHGlobal project, then compare every revealed field with this draft.
- [ ] Verify GitHub is public and final commits are pushed.
- [ ] Record and upload a 2–4 minute demo at 720p or higher, with a human voice and no sped-up footage, if time permits.
- [ ] Replace simulated UI captures with real iPhone captures for claims demonstrated on hardware.
- [ ] Confirm a working public demo link or provide clear build/run steps.
- [ ] Select only partner prizes supported by a working integration, if any.
- [ ] Submit by **2026-09-27 09:00 JST** and check the dashboard for confirmation.

The deadline, video guidance, track rules, AI disclosure, and prize limit come from [ETHGlobal's Tokyo 2026 submission guide](https://ethglobal.com/events/tokyo2026/info/details). The create-project sidebar still displays a generic “start from scratch” rule, while the event guide describes continuity tracks; use the event-specific track rules when making that selection.
