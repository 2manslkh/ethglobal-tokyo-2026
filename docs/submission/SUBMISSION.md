# tagtag — ETHGlobal Tokyo 2026 submission

Copy for the [ETHGlobal project form](https://ethglobal.com/events/tokyo2026/project). Prepared 2026-09-26 JST from the repository and the signed-in form. The **tagtag** project draft has the name, Gaming category, and 📍 emoji. Project details, images, tech stack, judging choices, and future opportunities were saved in the form. The project has **not** been submitted for judging: the finalist demo video is required and still missing.

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
| Source code | https://github.com/2manslkh/ethglobal-tokyo-2026 — selected in the form; the public page returned HTTP 200 on 2026-09-26. Push the final commit before submitting. |
| Demonstration link | Use the existing [TestFlight invitation](https://testflight.apple.com/join/cUnAbdSY), per the owner's choice. The recorded external build is **Waiting for Review** and predates the latest location and AR changes; keep that limitation visible in the description. |
| Demo video | To be recorded. The finalist Video step is required and blocks final submission. See [DEMO_VIDEO.md](DEMO_VIDEO.md). |
| Logo | [logo.png](logo.png), the actual 512×512 app icon; uploaded. |
| Cover image / project banner | [cover.png](cover.png), a 1672×941 promotional illustration generated for this submission with the app icon as a Taggi reference; uploaded. |
| Screenshots | [01-login-simulator.png](screenshots/01-login-simulator.png), [02-sticker-book-simulated.png](screenshots/02-sticker-book-simulated.png), and [05-confirm-spot-simulator.png](screenshots/05-confirm-spot-simulator.png) uploaded. Six prepared PNGs and their provenance are in [SCREENSHOTS.md](SCREENSHOTS.md). |
| Figma | Add a public design link if desired and available; no verified URL was found in the current repo. |

## Saved form answers

**Project details:** The TestFlight invitation, short description, project description, how-it's-made explanation, and GitHub repository were saved. The TestFlight link does not yet deliver the latest build.

**Tech stack:** These were saved in the form. The Sepolia souvenir code is disabled and was disclosed in the project description.

| Question | Saved answer |
| --- | --- |
| Ethereum developer tools | None of the listed tools. Thirdweb and `viem` are present only in the disabled staging path. |
| Blockchain networks | Ethereum (Sepolia is the target network of the disabled souvenir path). |
| Programming languages | C#, JavaScript, Swift, Solidity, Objective-C, C++. The native bridge uses Objective-C++. |
| Web frameworks | None. |
| Databases | Firebase (Firestore). |
| Design tools | None of the listed tools was verified. |
| Other technologies | Unity, AR Foundation, ARKit, MapKit, Firebase Authentication, Google Cloud Run, Google Cloud Storage, Sharp. |
| AI use (free text) | “Codex assisted with C#, native iOS, backend, contract, tests, documentation, and visual review under human direction. OpenAI imagegen created Taggi poses 5–12 from the supplied mascot reference; prompts and provenance are in docs/mascot/TAGGI_PRESETS.md. The original four poses and login video were supplied. The submission cover was generated with imagegen from the app icon. Source history, plans, and verification records are public in the repository.” |

**Future:** Interested in grant programs and accelerator/incubator programs.

## Partner prizes and judging

The saved choices are **Building from Scratch** and **Top 10 Finalist & Partner Prizes**. All partner prize boxes and the optional other-partner-technology picker were left empty. The form permits up to three partners and does not state a minimum.

**Partner prize answer: none.** Leave the partner checkboxes and the optional “other partners' technologies” picker empty. The disabled Sepolia/Thirdweb path does not establish a live partner integration. A 2–4 minute demo video is required for the selected finalist stream.

## Track and provenance

The repository's first commit is `0593383` at **2026-09-25 21:19 JST**. Check the official event start time before choosing **From Scratch**; if any project-specific code, designs, or assets predate the event, choose the appropriate continuity track and describe them. The first four Taggi poses and login video were supplied; the eight additional Taggi poses were generated during the event. The project history contains small feature commits and the design, planning, and verification records.

AI tools assisted implementation, documentation, and visual review. The eight new Taggi preset images were generated with OpenAI imagegen using the supplied mascot as reference; prompts and process are in [TAGGI_PRESETS.md](../mascot/TAGGI_PRESETS.md). UI and AR source provenance is recorded in [UI SOURCE_PROVENANCE.md](../../Assets/Tagtag/UI/SOURCE_PROVENANCE.md) and [AR SOURCE_PROVENANCE.md](../../Assets/Tagtag/AR/SOURCE_PROVENANCE.md). The repository's `docs/plans/`, `docs/verification/`, and commit history document human direction, testing, and iteration. Disclose the AI-assisted areas in the final form as requested by [ETHGlobal's rules](https://ethglobal.com/events/tokyo2026/info/details).

## Submission check

- [x] Confirm the track: Building from Scratch; disclose supplied Taggi assets and video.
- [x] Create the ETHGlobal project draft and inspect all steps.
- [x] Complete project details, tech stack, and images; save **Top 10 Finalist & Partner Prizes** with no partner prizes selected.
- [x] Verify GitHub is public (HTTP 200 on 2026-09-26); push the final submission commit before submitting.
- [x] Save interest in grants and accelerators.
- [ ] Record and upload a 2–4 minute demo at 720p or higher, with a human voice and no sped-up footage if seeking finalist prizes.
- [ ] Replace simulated UI captures with real iPhone captures for claims demonstrated on hardware.
- [ ] Confirm a working public demo link or provide clear build/run steps.
- [x] Decide partner prizes: none for this submission.
- [ ] Review the Final step's event-rule attestation, then submit by **2026-09-27 09:00 JST** and check the dashboard for confirmation.

The deadline, video guidance, track rules, AI disclosure, and prize limit come from [ETHGlobal's Tokyo 2026 submission guide](https://ethglobal.com/events/tokyo2026/info/details). The create-project sidebar still displays a generic “start from scratch” rule, while the event guide describes continuity tracks; use the event-specific track rules when making that selection.
