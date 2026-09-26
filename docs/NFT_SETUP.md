# Sticker NFTs

The approved [NFT plan](plans/2026-09-26-nfts.md) adds one transferable ERC-721 souvenir for each **new** app-approved discovery on Sepolia (`11155111`). The book and private notes remain app-based. A backend queue pays gas and retries independently of the phone. Collection succeeds before chain confirmation; the app refreshes pending NFT status every 15 seconds while foregrounded.

## Wallets and privacy

Thirdweb Unity SDK 6.1.3 creates an embedded EOA using the existing Firebase ID token. Enable custom JWT authentication in the Thirdweb project for issuer `https://securetoken.google.com/<firebase-project-id>`, audience `<firebase-project-id>`, and Firebase's signing keys. Restrict the public client ID to the app's bundle ID, `com.kenk.tagtag.staging` for staging (`com.kenk.tagtag` for production), using the provider's application settings. Verify the same Firebase UID restores the same address on two devices before enabling rollout.

The app signs a five-minute, single-use wallet-binding challenge; the authenticated API verifies it and prevents silent replacement or cross-account address reuse. No wallet private key enters app configuration or the API. The SDK's temporary authentication file uses iOS complete data protection and is deleted after authentication; sign-out disconnects the wallet. SDK provenance and storage details are in [Thirdweb runtime](../Assets/Thirdweb/README.md).

On-chain token IDs are random. Metadata includes only generic Taggi art and descriptions. Custom sticker discoveries use the first generic Taggi preset for their NFT; custom artwork remains in the app. Firebase IDs, authored text, notes, world maps, coordinates, and discovery timestamps are not NFT metadata. Transaction timing and recipient wallet addresses are public. App-approved discovery remains a gameplay check, not cryptographic proof of presence. Transferring a token does not grant note access or change the original collector's book.

**Account deletion stays available with informed fallback.** The approved plan's Operational defaults establish that minted NFTs are not erased; the user's latest update adds transfer-out first. The deletion screen accepts an external Ethereum address and sends individual confirmed NFTs using the connected embedded wallet. Transfers require Sepolia test ETH in that wallet; minting remains sponsored. Transfer intent is persisted before submission and transaction hashes survive restarts. Pending or uncertain sends are not automatically submitted again. Finalized receipt checks mark completion; uncertain sends can be checked through the wallet explorer. Deleting Firebase sign-in may make pending mints or NFTs left behind inaccessible, so the user must explicitly acknowledge that risk before deleting. There is no private-key export or marketplace UI.

## Configure and deploy

1. Follow the [Tokyo rollout record](NFT_TOKYO_ROLLOUT.md) for the owner's selected `tagtag-tokyo-2026` project, run the tests below and configure Thirdweb custom JWT authentication. The earlier [isolated staging setup](NFT_STAGING.md) remains a reference, not the current deployment target.
2. Build the four generic JSON/PNG assets with `python3 scripts/nft-staging/build-hosting.py` and deploy the dedicated Firebase Hosting configuration. Verify all eight public HTTPS URLs. Record the four versioned metadata URLs; no user content is published. Retain earlier versions on every hosting deployment.
3. Follow [contract deployment](../contracts/README.md). Use separate administrator and minter addresses, fund only dedicated Sepolia accounts, and deploy the non-upgradeable contract. Record the address, deployment transaction, and preset URIs in the deployment record.
4. Configure the selected project's HTTP API with `NFT_ENABLED=true`, `NFT_CHAIN_ID=11155111`, `NFT_CONTRACT_ADDRESS`, and its `NFT_WALLET_DOMAIN`. Deploy the Firestore indexes. The HTTP API receives no signer private key. Keep the NFT API revision at zero traffic until device acceptance.
5. Run a separate Cloud Run job from the backend image with command `node src/mint-worker.js`. In addition to the API's NFT configuration, provide `NFT_RPC_URL` and inject `NFT_SIGNER_PRIVATE_KEY` from Secret Manager. Give only this worker identity access to that secret. Use the selected Firebase project and bucket settings required by the adapter. Configure one task and one parallel execution; schedule it every minute after the manual mint check. Application leases also protect overlapping executions.
6. Set `nftEnabled=true` and the public `thirdwebClientId` in the Unity service configuration only for the verification build. Keep the shipped default disabled until the live checklist passes.

Worker setup and failure behavior are detailed in [backend instructions](../backend/README.md). Monitor pending-job age, delayed jobs, worker failures, and the dedicated signer's Sepolia ETH balance. Pausing contract minting or disabling the worker preserves queued work. Do not replace the configured contract address while there are pending jobs; a new contract needs an explicit migration.

## Verification

```sh
cd backend
npm ci
npm test
cd ../contracts
./install-deps.sh
forge test --offline
```

Run the README's Unity Edit Mode command for wallet lifecycle, cache compatibility, and presentation tests. Run graphics-enabled Play Mode with `-testFilter Tagtag.Tests.PaperVisualTests.NftStatusUpdatesWithoutReplacingPrivateNotes` for pending, confirmed, and wallet screenshots. Prepare ARKit in a separate invocation, export iOS, then compile with Xcode as documented in the README.

Live acceptance requires two physical iPhones and independently signed-in accounts: publish, discover, collect immediately, bind the embedded wallet, confirm exactly one NFT on Sepolia, restore the same wallet on the second device, and retry collection without minting another token. Check provider outage, unfunded signer, sign-out during provisioning, and account deletion acknowledgement. Fund the embedded wallet with Sepolia test ETH, transfer one NFT through the deletion screen, and verify finalized ownership at the destination. Check insufficient transfer gas and app restart during a pending transfer. Transfer must leave private-note authorization unchanged.

Live minting is not verified until Thirdweb configuration, Firebase Hosting deployment, a funded signer, a deployed contract, and device checks are recorded in [device verification](DEVICE_VERIFICATION.md). Local mocks and an unsigned Xcode build do not establish that live flow.
