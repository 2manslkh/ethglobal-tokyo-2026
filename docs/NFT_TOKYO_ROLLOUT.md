# Sepolia NFT rollout in tagtag-tokyo-2026

The owner selected the existing billed Google Cloud/Firebase project
`tagtag-tokyo-2026` in place of the isolated `tagtag-nft-staging-2026` project.
This changes the deployment target of the [NFT setup](NFT_SETUP.md). The live
API still serves its prior revision; the NFT-enabled API is tagged at zero
traffic. A test app build targets that candidate revision. Only new collections
made after enablement enqueue an NFT.

## Public metadata

The default Firebase Hosting site `tagtag-tokyo-2026.web.app` had no release
before this rollout. Release `sites/tagtag-tokyo-2026/releases/1790424129738000`
published eight generic assets under `/nft/v1/`. All four JSON and four PNG
responses returned HTTP 200 with the expected content types and bytes matching
`hosting/nft-public-production/`. The metadata JSON points to images on the
same site. These URLs must remain available after tokens are minted.

To build the artifacts again without changing existing versioned content:

```sh
python3 scripts/nft-staging/build-hosting.py \
  --origin https://tagtag-tokyo-2026.web.app \
  --output hosting/nft-public-production
```

`firebase.nft-production.json` scopes future Hosting deploys to this site's
generic NFT assets. Preserve all earlier version directories in future releases.

## Chain and worker preparation

- Sepolia RPC: Thirdweb client-ID endpoint returned chain ID `11155111`.
  Secret `tagtag-sepolia-rpc-url`, version 1, stores that URL for the worker.
- Contract admin and deployer: `0x2e1A2df6A223275a9C8d564299968B2C500ccdfd`.
  Its key is in Secret Manager secret `tagtag-sepolia-admin`, version 1.
- Mint signer: `0x62f107A76ff88B59A3040B2C1220538f42ee7292`.
  Its key is in secret `tagtag-sepolia-minter`, version 1.
- Service account `tagtag-nft-mint@tagtag-tokyo-2026.iam.gserviceaccount.com`
  has Firestore data access and secret access only to the minter and RPC
  secrets. It has no binding on the admin secret. No project-wide
  `roles/secretmanager.secretAccessor` binding was observed.
- The admin/deployer received 0.1 Sepolia ETH and the mint signer received
  0.5 Sepolia ETH before contract deployment.
- Contract `0x7Ce09FE8c8CCD140998b8517de6602b94590A756` deployed on Sepolia
  in transaction `0xe36d43b93b259aa7109573632ef5d6ae0962dc71b3eb8549f925ee7ad6f5b046`.
  Its receipt succeeded, code is nonempty, the admin and minter role checks
  passed, and `mintPaused` is false.
- All three `nftMints` composite indexes reached `READY`.
- The `tagtag-nft-mint` Cloud Run job uses a separate service account, the
  deployed API image, and pinned secret versions. It has no scheduler yet.
  Manual execution `tagtag-nft-mint-k8czq` succeeded with
  `{"acquired":true,"processed":0}` against the empty queue. An earlier
  execution failed because the shared Firebase adapter also required the
  public bucket name and Firebase API key; those settings were added.
- API revision `tagtag-api-00023-yoq` has the NFT configuration and the
  `nft-candidate` tag at zero traffic. `/health` returned 200 and anonymous
  `/v1/wallet` returned 401. The existing live revision kept 100% traffic.

Do not grant the existing API service account access to the signer secret. The
API receives only `NFT_ENABLED`, chain ID, contract address, and wallet domain;
the worker receives the signer and RPC secrets. The contract's four constructor
URIs are `https://tagtag-tokyo-2026.web.app/nft/v1/taggi-1.json` through
`taggi-4.json`.

## Remaining acceptance

Complete Thirdweb custom JWT authentication for Firebase issuer
`https://securetoken.google.com/tagtag-tokyo-2026` and audience
`tagtag-tokyo-2026`, allowing the production bundle `com.kenk.tagtag`. The
local test build uses `nftEnabled=true`, the public Thirdweb client ID, and
the zero-traffic candidate API URL. Unity Edit Mode passed 255/255 after fixing
three stale publication-test fixtures. Separate ARKit preparation, Unity iOS
export, and signed Xcode Debug/iphoneos build succeeded. CoreDevice installed
and launched the app over the existing installation on Dawg. (iPhone 15 Pro
Max); no app interaction or wallet result has yet been observed. Verify one
new discovery in the app produces one finalized ERC-721 on Sepolia, with the
collector's embedded wallet as owner and the expected token URI. Record the
transaction, token ID, wallet address, app/device behavior, and retry result in
[device verification](DEVICE_VERIFICATION.md). Then promote the candidate API,
schedule the mint job, and build with the public production API URL. No live
mint is yet claimed.
