# NFT staging verification

Source baseline `a0ee368`; implementation isolated on `feat/nft-staging`.

## Local checks

- `cd backend && npm test`: 79 passed, zero failed, one emulator-only test skipped.
- Firebase CLI 14.21.0 Auth/Firestore/Storage emulator, project `demo-tagtag`,
  `cd backend && npm run test:emulator`: 1/1 passed.
- `cd contracts && forge test --offline`: 9/9 passed, including HTTPS metadata
  selection, transfer preservation, duplicate mint rejection, and Sepolia gating.
- `python3 -m unittest discover -s scripts/nft-staging -p 'test_*.py' -v`:
  16/16 passed (14 infrastructure, two public asset tests).
- Firebase Hosting emulator using `firebase.nft-staging.json`: all eight public
  JSON/PNG URLs returned HTTP 200, expected content types, and bytes identical to
  the local artifacts. Loopback port 5017 avoids macOS's occupied port 5000.
- [Edit Mode](edit-mode.xml): 226/226 passed, including nine staging configuration
  checks for isolation, matching Firebase plist credentials, and rejected input.
- [NFT Play Mode](play-mode.xml): 1/1 passed for
  `Tagtag.Tests.PaperVisualTests.NftStatusUpdatesWithoutReplacingPrivateNotes`.
- GPT-6-Astra/high read-only review: no remaining Important/Critical findings after
  API-key project ownership and first-deployment missing-job handling fixes.

Unity tests used Editor 6000.5.5f1 and StandaloneOSX in the isolated worktree.
Production `ServiceConfiguration.json` and PlayerSettings hashes were unchanged.
Concurrent header/sticker work in the main checkout was not edited or built here.

## Pending live checks

Billing project quota blocks the staging deployment; the user requested local
completion with deployment pending. Firebase project/Auth/provider configuration,
hosting URLs, contract, signer funding/secrets, worker scheduling, and live device
mint/restore/transfer remain unverified. Public Thirdweb client ID availability
does not establish successful JWT authentication. See [staging setup](../../NFT_STAGING.md).

The production project's alert budget was separately changed to S$1/month at the
user's request and read back successfully. This does not change the project quota
or enforce a spending cap.
