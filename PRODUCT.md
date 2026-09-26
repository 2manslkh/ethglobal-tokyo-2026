# Product

<!-- impeccable:product-schema 1 -->

## Platform

ios

## Stack

Unity 6000.5.5f1, UI Toolkit, AR Foundation/ARKit, MapKit, Firebase Authentication, Firestore, Cloud Storage, and Node.js on Cloud Run.

## Users

People exploring places and leaving personal recommendations for future visitors. Open account signup; iPhone first.

## Product Purpose

See a teaser, visit a place, find and tap its AR sticker, unlock its note, and collect a copy. The original remains available for others.

## Capabilities and Constraints

Home is a 5×4 paginated sticker book. STICK combines discovery and preset placement. Explore shows street-map locations and teasers. Publishing and collecting require Apple or Google sign-in. Notes remain protected until the AR discovery flow completes. Collections sync across devices and support offline viewing.

New discoveries support automatic transferable souvenir NFTs on Ethereum Sepolia, behind a rollout flag. Embedded wallets use the existing Firebase sign-in, and the backend pays minting gas. Existing collections are not backfilled; chain failures never block note access or collection. NFTs contain only generic mascot artwork, not private notes or locations. Before account deletion, users can transfer individual NFTs to an external Ethereum address using Sepolia test ETH in their embedded wallet. Deletion remains available after an explicit acknowledgement that pending mints or NFTs left behind may become inaccessible. No key-export UI, marketplace, mainnet, Android, or App Store submission in this release. Hosting targets less than US$10/month; budget alerts are not hard caps and the wallet provider is a separate service.

## Brand Commitments

tagtag is the app; Taggi is the hand-drawn rabbit-like mascot. Paper-white, hand-drawn, die-cut sticker direction. Four supplied poses seed the preset library.

## Accessibility & Inclusion

Readable text scaling, accessible labels, safe areas, 44-point controls, reduced motion, and clear permission/recovery states.
