#!/bin/sh
set -eu

contract_root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
forge install --no-git --root "$contract_root" \
  OpenZeppelin/openzeppelin-contracts@rev=c64a1edb67b6e3f4a15cca8909c9482ad33a02b0
