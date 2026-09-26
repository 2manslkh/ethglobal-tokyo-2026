# Sepolia NFT rollout in tagtag-tokyo-2026

The owner selected the existing billed Google Cloud/Firebase project
`tagtag-tokyo-2026` in place of the isolated `tagtag-nft-staging-2026` project.
This changes the deployment target of the [NFT setup](NFT_SETUP.md). A test app
build targets the `nft-candidate` API tag. On 2026-09-27, a later deployment
placed NFT-enabled revision `tagtag-api-00024-dub` at 100% public traffic and
moved that tag to the same revision. Only new collections made after enablement
enqueue an NFT. A five-minute mint schedule was enabled after the first live
mint transaction succeeded.

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
  deployed API image, and pinned secret versions. It initially had no scheduler.
  Manual execution `tagtag-nft-mint-k8czq` succeeded with
  `{"acquired":true,"processed":0}` against the empty queue. An earlier
  execution failed because the shared Firebase adapter also required the
  public bucket name and Firebase API key; those settings were added.
- API revision `tagtag-api-00023-yoq` initially had the NFT configuration and
  the `nft-candidate` tag at zero traffic. `/health` returned 200 and anonymous
  `/v1/wallet` returned 401. A subsequent API deployment created
  `tagtag-api-00024-dub` with the same NFT environment settings and a newer
  image. It now has 100% public traffic and the `nft-candidate` tag. The prior
  revision is retired.
- Scheduler job `tagtag-nft-mint-every-5m` runs `tagtag-nft-mint` every five
  minutes in `asia-northeast1`. Its dedicated OAuth identity
  `tagtag-nft-scheduler@tagtag-tokyo-2026.iam.gserviceaccount.com` has
  `roles/run.invoker` only on the mint job and no signer-secret access.
  An explicit Scheduler dispatch created execution `tagtag-nft-mint-dp8gt`,
  which completed successfully.

Do not grant the existing API service account access to the signer secret. The
API receives only `NFT_ENABLED`, chain ID, contract address, and wallet domain;
the worker receives the signer and RPC secrets. The contract's four constructor
URIs are `https://tagtag-tokyo-2026.web.app/nft/v1/taggi-1.json` through
`taggi-4.json`.

## Remaining acceptance

The wallet has changed to a 12-word BIP-39 phrase generated and stored on the
iPhone. Thirdweb custom JWT authentication is no longer required. The public
client ID is used for Sepolia RPC only. The local test build uses
`nftEnabled=true`, that public RPC client ID, and
the `nft-candidate` API URL. The original JWT build passed Unity Edit Mode
255/255 after fixing
three stale publication-test fixtures. Separate ARKit preparation, Unity iOS
export, and signed Xcode Debug/iphoneos build succeeded. CoreDevice installed
and launched the app over the existing installation on Dawg. (iPhone 15 Pro
Max). On 2026-09-27, CoreDevice captured the app open on its Your Note form,
but no wallet result or collection had been observed. The `wallets` and
`nftMints` Firestore collections were both empty at that check. The replacement
phone-wallet build passed 258/258 Edit Mode tests, exported and signed for iOS,
and was installed and launched on Dawg. Its first launch produced one verified
wallet binding, address `0xBC5fc5e8EBd5611DdE4b56C88236F5878E85bACb`.
The collector then completed a new discovery in the app. Worker execution
`tagtag-nft-mint-ncpd8` submitted transaction
`0xd13cee7be67abf116654c1f92afeb208432a6ad6fe3f381ae4443644895f6b05`;
its receipt succeeded and `ownerOf` returned the phone wallet. The token URI
matched the first generic Taggi metadata. Once Sepolia finalized its block,
worker execution `tagtag-nft-mint-g4wkv` marked the Firestore mint `confirmed`
with one signing attempt. A worker fix prevents rebroadcasting a mined receipt
while waiting for finality; the mint job now uses the corrected image, and the
backend suite passed 85 tests with one emulator-only skip. The collected-sticker
sheet then displayed “Souvenir NFT minted on Sepolia” on Dawg. after its next
successful refresh. Second-device recovery remains to verify.
See [device verification](DEVICE_VERIFICATION.md) for the token ID and device
record. The current local test build still uses the candidate API URL, which
resolves to the same live revision as the public production URL.
