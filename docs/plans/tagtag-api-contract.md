# tagtag integration contract

All work uses `Tagtag` namespaces. Shared C# types and interfaces are in `Assets/Tagtag/Core/TagtagModels.cs`; coordinator owns this file. JSON uses their lower-camel-case field names. Seconds are Unix seconds. Preset IDs: taggi-1, taggi-2, taggi-3, taggi-4. Preset textures load as Resources `Tagtag/Presets/taggi-1` etc (Texture2D). No secret configuration is bundled.

## Component entry points

UI: `Tagtag.UI.TagtagAppView : MonoBehaviour`, `Initialize(ITagtagController controller)`.
AR: `Tagtag.AR.ArExperience : MonoBehaviour, IArExperience`.
Map: `Tagtag.AR.NativeMapView : MonoBehaviour, IMapExperience`. Show Rect uses Unity Screen bottom-left pixel coordinates. UI should call Show only with usable map region and hide on screen departure.
Identity: `Tagtag.AR.NativeIdentity : MonoBehaviour, INativeIdentity`. Apple/Google credentials feed Firebase accounts:signInWithIdp; session persistence uses iOS Keychain. Native plugin callbacks must not leak credentials in logs.

Coordinator composes these through a TagtagApplication MonoBehaviour, handles Firebase REST sign-in/token refresh, HTTP clients, location, cache, publication, and collection. Components must not use a second independent service configuration or application state.

## HTTP /v1

Success JSON object; errors `{error:{code,message}}`. Authorization uses Bearer Firebase ID token. Public nearby browsing accepts no token, filters authenticated users' blocks when provided. Every state-changing operation authenticates.

- `POST /v1/nearby` body `{location:LocationFix}` → `{items:StickerSummary[]}`. Summaries never include note or map.
- `POST /v1/publications/prepare` body `{operationId,presetId,place,teaser,note,location,position:{x,y,z},rotation:{x,y,z,w},widthMeters,mapBytes}` → `{id,uploadUrl,uploadHeaders}`. uploadHeaders is a JSON object; signed upload is binary world-map octets (not base64). Max map 16 MiB. Coordinator strips base64 from this request and sends map bytes separately.
- `POST /v1/publications/{id}/finalize` body `{operationId,location}` → `{sticker:StickerSummary}`. Upload metadata verified before publication becomes visible. Operations idempotent per authenticated owner+operationId.
- `POST /v1/stickers/{id}/recover` body `{location}` → `{sticker:StickerSummary,discoveryId,expiresAt,mapUrl,position,rotation,widthMeters}`. Download world map separately; client constructs RecoveryData.snapshot.
- `POST /v1/stickers/{id}/collect` body `{discoveryId,location}` → `{sticker:CollectedSticker}`. Requires matching short-lived discovery session and proximity. AR tap/distance gate stays client-side; never describe it as unforgeable proof. Idempotent per user+sticker.
- `GET /v1/collection` → `{items:CollectedSticker[]}`. Moderated/deleted content has `unavailable:true`, empty note. Ordinary withdrawn content remains in existing collections.
- `GET /v1/authored` → `{items:StickerSummary[]}`.
- `POST /v1/stickers/{id}/withdraw` body `{}` → `{ok:true}`.
- `POST /v1/stickers/{id}/report` body `{reason}` → `{ok:true}`.
- `POST /v1/blocks` body `{authorId}` → `{ok:true}`.
- `DELETE /v1/account` → `{ok:true}`. Remove authored content, own collection and Firebase user; revoke other users' note access.
- `GET /health` → readiness without credentials/content.

## NFT extension

NFT support is opt-in at deployment and Sepolia-only. Legacy collection entries omit `nft` and are never backfilled. New eligible collections atomically create a mint outbox entry, even if their embedded wallet is not ready. Collect/recover authorization and note access remain unchanged.

- `GET /v1/wallet` → `{enabled,address,chainId}`; authenticated, empty address until bound.
- `POST /v1/wallet/challenge` body `{address}` → `{challengeId,message,expiresAt}`; five-minute, account-bound, single-use EIP-191 challenge.
- `POST /v1/wallet/bind` body `{challengeId,signature}` → wallet status; verifies ownership, reserves one address per account, and rejects replacement or reuse across accounts.
- `CollectedSticker.nft` → `{status,chainId,contractAddress,tokenId,transactionHash}` when a mint job exists. Public statuses: `pending`, `confirmed`, `delayed`, `cancelled`. Token IDs are decimal strings; chain ID is `11155111`. No transaction payloads or signing credentials are returned.

Unity configuration adds `nftEnabled` (default false) and `thirdwebClientId` (public). Wallet setup and mint status run separately from gameplay busy/error state. The app refreshes pending mint status while foregrounded. The optional `INftTransferController` UI boundary supports standard EOA transfer-out, receipt refresh, and explicit NFT-loss acknowledgement before deletion. Transfer state is account-specific local data; no new backend transfer authority is granted. Account deletion remains available with an informed NFT access-loss fallback; minted tokens remain on-chain. See [setup and rollout](../NFT_SETUP.md).

Admin custom claim `admin:true`: report list and moderation actions; agent may define private admin wire details and document under backend.

Defaults: location ≤30s old; publication prepare/finalize accept accuracy ≤100m, while nearby/recovery/collection require ≤50m; recovery/collection within 100m of placement; note ≤2000 chars, teaser ≤180, place ≤80; 5 publications/user/day; bounded nearby results (100). Never return coordinates/notes/tokens in request logs. Maps private in bucket. Presets have no uploads from end users. Published original persists after collection. Backend agent adds tests with injected adapters and emulator integration where available.

## Ownership and coordination

UI agent owns UI/Resources art. AR agent owns AR/native/Packages/Editor build. Backend agent owns backend and Firebase config. Coordinator owns Core/Services/Application and docs. Do not run Unity concurrently; coordinator performs combined Unity builds. Do not stage, commit, reset, or revert another worker's changes. Tell coordinator when ready for integration. Deliverables use tagtag branding and preserve required third-party license notices.
