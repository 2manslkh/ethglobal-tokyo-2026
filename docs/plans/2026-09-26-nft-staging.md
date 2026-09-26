# NFT staging activation

Approved in conversation: activate the existing Thirdweb/Firebase embedded wallet and automatic Sepolia NFT flow in a separate staging environment. Retain iOS 15. Replace IPFS with public Firebase Hosting metadata and images. New discoveries only; no backfill, marketplace, or production activation.

## Ownership

Worktree `/private/tmp/tagtag-nft-staging`, branch `feat/nft-staging`. Herdr workspace NFT staging; preserve user focus.

- Staging backend tab: GPT-6-Sol/high; owns infrastructure scripts under `scripts/nft-staging/`, infrastructure examples under `config/nft-staging/`, and backend documentation. Coordinator owns hosting generator in that scripts directory.
- Staging iOS tab: GPT-6-Sol/high; owns build environment helpers/tests, `BuildIos.cs`, staging build wrapper, and service configuration example. Coordinator runs all Unity checks.
- Coordinator: Firebase Hosting assets/configuration and generator, contracts, cloud operations, documentation, integration and builds.
- Security review: GPT-6-Astra/high, read-only review of the final changes.

Agents request an exclusive Git slot before staging/committing. Coordinator verifies and pushes integrated commits. Preserve existing checkout changes.

## Deliverables

1. Isolated `tagtag-nft-staging-2026` project in `asia-northeast1`: Firebase Auth/Firestore/private storage, Cloud Run API and cleanup, dedicated mint worker identity and scheduler. Production remains unchanged.
2. Four generic metadata JSON files and PNGs at versioned Firebase Hosting URLs. Preserve published content. No personal content in public assets. Existing ERC-721 constructor accepts HTTPS URIs.
3. Separate `com.kenk.tagtag.staging` build, explicit external public configuration, fail-closed production isolation, `Build/iOS-staging` output, and restoration after export errors.
4. Thirdweb JWT project configuration, dedicated Sepolia admin/minter and deployed contract, worker-only signer secret. Enable minting only after prerequisites pass.
5. Backend/emulator/contract/Unity/native checks, then two-device mint/restore/transfer acceptance. Record missing access or hardware honestly.

## Progress

- Source baseline: main at implementation start; original checkout has unrelated TODO and SceneTemplateSettings changes.
- Staging project created, project number `542095619867`.
- User supplied public Thirdweb client ID `639af3b16477c0bb4b73b8e163eb0397`; live JWT configuration remains unverified.
- Fresh worktree initially has no installed Node/contract dependencies; install locked dependencies before baseline tests.
- Billing attachment failed with project quota exhaustion. User explicitly chose to finish local setup and leave deployment pending. No contract, wallet, signer key, or secret was created. Firebase CLI registration could not authenticate.
- Backend baseline: 79 passed, one emulator-only test skipped. Separate Auth/Firestore/Storage emulator: 1/1 passed. Contracts: 9/9 passed.
- Public Hosting emulator: all eight JSON/PNG URLs return 200 with correct content types and exact artifact bytes. Default port 5000 belongs to macOS Control Center; dedicated emulator config uses loopback port 5017.
- Unity verification coordinated with header task on main; no main-project builds or index writes from this worktree.
- Completed: 16 staging tooling tests, 226 Unity Edit Mode tests, one NFT Play Mode fixture, isolated iOS export and unsigned Xcode build. Compiled with clearly nonfunctional auth fixtures; live authentication is not verified. Source configuration hashes unchanged.
- Security review resolved missing-job bootstrap and Firebase project ownership findings; no remaining Important/Critical findings. Atomic commits pushed on `feat/nft-staging`; draft PR #3.
- Separate user request completed: production tagtag alert budget changed from S$10 to S$1/month and read back, preserving 10/50/100% thresholds. This does not unblock staging or impose a spend cap.
