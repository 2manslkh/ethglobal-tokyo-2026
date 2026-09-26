// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {AccessControl} from "@openzeppelin/contracts/access/AccessControl.sol";
import {ERC721} from "@openzeppelin/contracts/token/ERC721/ERC721.sol";

/// @notice Transferable souvenirs for Taggi discoveries. Metadata contains only a public pose.
contract TagtagSouvenir is ERC721, AccessControl {
    error InvalidAdmin();
    error InvalidMinter();
    error InvalidRecipient();
    error InvalidPreset(uint8 preset);
    error EmptyPresetUri(uint8 preset);
    error MintPaused();

    bytes32 public constant MINTER_ROLE = keccak256("MINTER_ROLE");

    string[4] private _presetUris;
    mapping(uint256 tokenId => uint8 preset) private _tokenPresets;
    bool public mintPaused;

    constructor(address admin, address minter, string[4] memory presetUris)
        ERC721("tagtag discoveries", "TAGTAG")
    {
        if (admin == address(0)) revert InvalidAdmin();
        if (minter == address(0)) revert InvalidMinter();

        for (uint8 preset = 0; preset < 4; preset++) {
            if (bytes(presetUris[preset]).length == 0) revert EmptyPresetUri(preset);
            _presetUris[preset] = presetUris[preset];
        }

        _grantRole(DEFAULT_ADMIN_ROLE, admin);
        _grantRole(MINTER_ROLE, minter);
    }

    function mint(address recipient, uint256 tokenId, uint8 preset) external onlyRole(MINTER_ROLE) {
        if (mintPaused) revert MintPaused();
        if (recipient == address(0)) revert InvalidRecipient();
        if (preset >= 4) revert InvalidPreset(preset);

        _tokenPresets[tokenId] = preset;
        _safeMint(recipient, tokenId);
    }

    function setMintPaused(bool paused) external onlyRole(DEFAULT_ADMIN_ROLE) {
        mintPaused = paused;
    }

    function presetOf(uint256 tokenId) external view returns (uint8) {
        _requireOwned(tokenId);
        return _tokenPresets[tokenId];
    }

    function tokenURI(uint256 tokenId) public view override returns (string memory) {
        _requireOwned(tokenId);
        return _presetUris[_tokenPresets[tokenId]];
    }

    function supportsInterface(bytes4 interfaceId)
        public
        view
        override(ERC721, AccessControl)
        returns (bool)
    {
        return super.supportsInterface(interfaceId);
    }
}
