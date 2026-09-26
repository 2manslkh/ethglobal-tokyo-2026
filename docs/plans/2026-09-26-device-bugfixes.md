# Device follow-up

The user reported unreadable Image Playground output, a gap below sheets, unclear publishing readiness, and requested sticker-app's discovery reference photo shared with other finders.

## Work ownership

- Native Image Playground: Herdr ar-scan, GPT-6-Sol high. Swift delegate and Objective-C bridge only.
- Sheet geometry: Herdr creator-flow, GPT-6-Sol high. SheetBottom/ApplySheetHeight in Account partial only; coordinator commits the shared file.
- Shared reference capture: Herdr design-api, GPT-6-Sol high. AR capture/lifecycle, bounded versioned WorldMapEnvelope, optional Core IReferencePhotoAr contract. No backend/API change.
- Coordinator: scan guidance, note return action, reference thumbnail/enlarged sheet, integration, build/install and documentation. Preserve TODO.md; serialize Git writes.

## Design

Read and own generated Image Playground bytes before dismissal and before crossing the asynchronous native callback. Anchor the sheet paper to the bottom edge, with the safe-area inset inside its padding; preserve keyboard avoidance. Explain multi-angle scanning on camera and in Write note, with Continue scanning retaining draft fields.

Reference behavior follows sticker-app's Original spot thumbnail and enlarged saved-photo sheet. The user explicitly chose sharing with finders. Capture only the AR camera scene, excluding UI; include a bounded JPEG in a version 2 private map envelope. Reuse map authorization, expiry, storage and deletion. New app reads version 1 maps without a photo; older app versions cannot read version 2 maps. Preview is a visual aid and never grants collection or indicates spatial recovery success. Disclose the surroundings photo in Write note.

## Verification

Build native Swift/Objective-C and Unity together, install on the connected iPhone, and record actual results. Image Playground generation, safe-area appearance and physical AR reference matching need device interaction; static diagnosis alone does not establish those outcomes.
