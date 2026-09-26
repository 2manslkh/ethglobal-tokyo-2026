# NFT staging

Staging keeps the existing Thirdweb embedded wallet, Firebase sign-in, and Sepolia
souvenir contract. NFT metadata and images are served directly by Firebase Hosting.
There is no IPFS service. Production remains disabled and is not a deployment target
of the staging tooling.

## Current state

- Google Cloud project: `tagtag-nft-staging-2026` (`542095619867`), Tokyo region.
- Billing attachment failed with **Cloud billing quota exceeded**. This is a
  billing-enabled project quota, not a monetary budget. The user requested local
  completion with deployment pending.
- Firebase project registration, Auth providers, storage/database, API, jobs,
  Hosting, contract deployment, and device verification remain pending.
- Thirdweb public client ID: `639af3b16477c0bb4b73b8e163eb0397`. Account creation
  does not establish that JWT authentication or wallet restoration works.
- Staging bundle: `com.kenk.tagtag.staging`; display name: **tagtag staging**.
  Minimum iOS remains 15. A separate identity avoids replacing the installed app.

## Provider setup

Add the staging Google Cloud project to Firebase after billing is resolved. Register
the staging iOS app and configure Apple and Google sign-in for its bundle ID. Use
the staging Firebase public API key and staging iOS Google OAuth client/reversed
client ID. Do not copy production Auth credentials or users.

Configure Thirdweb custom JWT authentication using the staging Firebase issuer
`https://securetoken.google.com/tagtag-nft-staging-2026`, audience
`tagtag-nft-staging-2026`, and Firebase's rotating public JWKS at
`https://www.googleapis.com/service_accounts/v1/jwk/securetoken@system.gserviceaccount.com`.
Use the JWT subject (`sub`, the Firebase UID) for stable identity. Restrict the
public client ID to the staging bundle in the provider settings. Verify expired
tokens, wrong-project tokens, and cross-device restoration before rollout.

Copy `config/nft-staging/ServiceConfiguration.example.json` to a local file outside
Git and fill its missing Firebase/Google fields. Its Thirdweb ID is public; never
put a provider secret, service-account key, or blockchain private key in this file.
Download the staging iOS app's `GoogleService-Info.plist` directly from Firebase
Console and keep it beside the local configuration. The build verifies its project
ID, project number, app ID, bundle ID, API key, and Google client values against
the selected staging configuration. A different key from production alone does
not prove that a key belongs to a different Firebase project.

## Public NFT assets

From the repository root:

```sh
python3 scripts/nft-staging/build-hosting.py
python3 scripts/nft-staging/test_hosting.py
npx --yes firebase-tools@14.21.0 deploy --only hosting \
  --config firebase.nft-staging.json --project tagtag-nft-staging-2026
```

This dedicated config publishes only `hosting/nft-public/`. Private map and design
storage rules are unchanged. The eight public files are under
`https://tagtag-nft-staging-2026.web.app/nft/v1/`: `taggi-1.json` through
`taggi-4.json`, plus their PNGs under `images/`. Each metadata response must be
JSON with `name`, `description`, and the corresponding public `image` URL. Verify
HTTP 200, correct content types, and PNG bytes without a Firebase login.

The generator refuses to overwrite different content at an existing version path.
Future artwork requires a new version directory, retaining all previous versions
in every deployment. Contract URIs are fixed, but HTTPS availability and content
are maintained by tagtag. Do not delete these assets during user account cleanup.

## Backend and contract

Follow the staging infrastructure section in [backend instructions](../backend/README.md)
to validate, preview, and apply the isolated deployment. Start with NFTs disabled.
The dedicated mint worker is the only runtime identity granted access to its
Secret Manager signer. Use numeric secret versions and keep keys outside command
arguments, source control, logs, and chat.

After hosting is verified, deploy [TagtagSouvenir](../contracts/README.md) to
Sepolia using separate administrator and minter addresses. Set preset URI 0 to
the hosted `taggi-1.json`, continuing through preset URI 3 and `taggi-4.json`.
Record the contract address, deployment transaction, four URLs, and signer
address. Fund dedicated accounts with Sepolia test ETH. Do not change the contract
address while mint jobs are pending.

Enable the staging API and worker only after Thirdweb authentication, contract
identity, signer role/funding, and metadata checks pass. New discoveries enqueue
one souvenir each; earlier collections are not backfilled. Custom sticker artwork
stays private and uses generic Taggi pose 1 for its NFT.

## Build and verify

```sh
scripts/build-ios-staging.sh /absolute/path/to/staging-service-configuration.json \
  /absolute/path/to/GoogleService-Info.plist
xcodebuild -project Build/iOS-staging/Unity-iPhone.xcodeproj \
  -scheme Unity-iPhone -configuration Debug -sdk iphoneos \
  -destination 'generic/platform=iOS' -derivedDataPath Build/StagingDerivedData \
  CODE_SIGNING_ALLOWED=NO build
```

The wrapper exports from a disposable project under `/private/tmp`, prepares ARKit
in a separate invocation, and leaves the source checkout's production resources
and PlayerSettings untouched. Use a provisioning profile supporting Sign in with
Apple for `com.kenk.tagtag.staging` before installation. A compiled fixture build
does not verify live provider configuration.

Run backend tests and Firebase emulators, `forge test --offline`, Unity Edit Mode
tests, and the NFT presentation Play Mode test documented in [NFT setup](NFT_SETUP.md).
On two physical iPhones, verify immediate collection, one finalized NFT, no duplicate
mint after retries, restored address, and finalized transfer ownership. Exercise
provider/RPC outage, insufficient funds, account switching, interrupted transfers,
and account deletion acknowledgement. Transferring a token must not grant private
note access. Record results in [device verification](DEVICE_VERIFICATION.md).

Monitor pending job age, delayed jobs, failed worker executions, and signer balance.
To stop new minting, disable only the API's `NFT_ENABLED` flag while the configured
worker continues reconciling submitted transactions. Do not treat a fully disabled
redeployment or paused worker as completed reconciliation. Production activation
requires a separate rollout after staging acceptance.
