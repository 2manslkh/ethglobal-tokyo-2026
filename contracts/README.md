# Tagtag souvenir contract

`TagtagSouvenir` is a transferable ERC-721 for new discoveries. Only an address
with `MINTER_ROLE` can mint. The admin can manage roles and pause new mints;
pausing does not block transfers. A token's preset (0–3) selects one of four
metadata URIs fixed at deployment. Token IDs cannot be reused, including after
a transfer. There is no burn, upgrade, or metadata editing method.

## Dependencies and tests

The project uses Solidity 0.8.24 and OpenZeppelin Contracts 5.4.0 at the exact
revision in `dependencies.lock`. Verification used the pinned Foundry revision
in that lock file. No `forge-std` library is required; the tests and script
declare only the cheatcodes they use. From this directory:

```sh
./install-deps.sh
forge test --offline
```

The dependency is installed into ignored `lib/`; generated Foundry files are
also ignored. `--offline` avoids Foundry's optional remote signature lookup
during local tests.

## Metadata

`metadata/taggi-1.json` through `metadata/taggi-4.json` contain only generic
pose names, descriptions, and public HTTPS image URLs. They contain no account,
location, note, or discovery data. Preset 0 maps to `taggi-1.json`, preset 1 to
`taggi-2.json`, and so on. Firebase Hosting serves metadata and PNGs under
`https://tagtag-nft-staging-2026.web.app/nft/v1/`. Hosting deployment is pending;
these URLs must return the expected JSON and images before contract deployment.

Run `python3 scripts/nft-staging/build-hosting.py` from the repository root to
verify/build `hosting/nft-public/`. The generator refuses to overwrite changed
versioned files. Future asset changes require a new version directory while
retaining prior versions in each Hosting deployment. HTTPS content availability
is operated by tagtag; fixed contract URIs do not make hosted bytes immutable.
See [staging setup](../docs/NFT_STAGING.md).

## Sepolia deployment

The deployment script rejects any chain other than Sepolia (11155111). Set
`TAGTAG_DEPLOYER_PRIVATE_KEY`, separate `TAGTAG_ADMIN` and `TAGTAG_MINTER`
addresses, and `TAGTAG_PRESET_URI_0` through `TAGTAG_PRESET_URI_3` to the verified
Firebase Hosting metadata URLs ending in `taggi-1.json` through `taggi-4.json`. Keep the key and signer credentials outside the repository.
After funding the deployer and reviewing these values, run:

```sh
forge script script/DeployTagtagSouvenir.s.sol:DeployTagtagSouvenir \
  --rpc-url "$SEPOLIA_RPC_URL" --broadcast
```

No deployment has been performed. The backend should use the dedicated minter
signer, and the admin address should be controlled separately.
