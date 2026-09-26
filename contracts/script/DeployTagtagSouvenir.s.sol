// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {TagtagSouvenir} from "../src/TagtagSouvenir.sol";

interface DeploymentVm {
    function envUint(string calldata name) external returns (uint256);
    function envAddress(string calldata name) external returns (address);
    function envString(string calldata name) external returns (string memory);
    function startBroadcast(uint256 privateKey) external;
    function stopBroadcast() external;
}

contract DeployTagtagSouvenir {
    DeploymentVm private constant VM =
        DeploymentVm(address(uint160(uint256(keccak256("hevm cheat code")))));

    function run() external returns (TagtagSouvenir souvenir) {
        require(block.chainid == 11155111, "Sepolia only");

        uint256 deployerPrivateKey = VM.envUint("TAGTAG_DEPLOYER_PRIVATE_KEY");
        address admin = VM.envAddress("TAGTAG_ADMIN");
        address minter = VM.envAddress("TAGTAG_MINTER");
        require(deployerPrivateKey != 0, "Missing deployer key");
        require(admin != minter, "Admin and minter must differ");

        string[4] memory presetUris;
        presetUris[0] = VM.envString("TAGTAG_PRESET_URI_0");
        presetUris[1] = VM.envString("TAGTAG_PRESET_URI_1");
        presetUris[2] = VM.envString("TAGTAG_PRESET_URI_2");
        presetUris[3] = VM.envString("TAGTAG_PRESET_URI_3");

        VM.startBroadcast(deployerPrivateKey);
        souvenir = new TagtagSouvenir(admin, minter, presetUris);
        VM.stopBroadcast();
    }
}
