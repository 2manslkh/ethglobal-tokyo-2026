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

Admin custom claim `admin:true`: report list and moderation actions; agent may define private admin wire details and document under backend.

Defaults: location ≤30s old and accuracy ≤50m; recovery/collection within 100m of placement; note ≤2000 chars, teaser ≤180, place ≤80; 5 publications/user/day; bounded nearby results (100). Never return coordinates/notes/tokens in request logs. Maps private in bucket. Presets have no uploads from end users. Published original persists after collection. Backend agent adds tests with injected adapters and emulator integration where available.

## Ownership and coordination

UI agent owns UI/Resources art. AR agent owns AR/native/Packages/Editor build. Backend agent owns backend and Firebase config. Coordinator owns Core/Services/Application and docs. Do not run Unity concurrently; coordinator performs combined Unity builds. Do not stage, commit, reset, or revert another worker's changes. Tell coordinator when ready for integration. Read-only existing Unity implementation is available for behavior/API inspection; generated deliverables must contain only tagtag branding and third-party licensing where required.
