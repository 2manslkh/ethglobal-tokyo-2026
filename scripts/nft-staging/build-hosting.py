#!/usr/bin/env python3
"""Build only public, generic NFT assets. Published version paths never change."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PRESETS = ROOT / "Assets/Resources/Tagtag/Presets"
ORIGIN = "https://tagtag-nft-staging-2026.web.app"


def build(output, origin=ORIGIN):
    files = {}
    for pose in range(1, 5):
        files[Path(f"nft/v1/images/taggi-{pose}.png")] = (PRESETS / f"taggi-{pose}.png").read_bytes()
        metadata = {
            "name": f"Taggi pose {pose}",
            "description": "A Taggi discovery souvenir from tagtag.",
            "image": f"{origin}/nft/v1/images/taggi-{pose}.png",
        }
        files[Path(f"nft/v1/taggi-{pose}.json")] = (json.dumps(metadata, indent=2) + "\n").encode()
    # Validate every existing file before writing anything. A new release needs
    # a new version directory, keeping all earlier token metadata available.
    for relative, content in files.items():
        destination = output / relative
        if destination.exists() and destination.read_bytes() != content:
            raise ValueError(f"Refusing to replace {relative}; publish a new version instead")
    for relative, content in files.items():
        destination = output / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(content)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "hosting/nft-public")
    parser.add_argument("--origin", default=ORIGIN)
    args = parser.parse_args()
    build(args.output, args.origin)
    print(f"Verified eight public NFT files in {args.output}")
