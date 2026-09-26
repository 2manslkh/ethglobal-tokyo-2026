// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {DeployTagtagSouvenir} from "../script/DeployTagtagSouvenir.s.sol";

interface DeployVm {
    function expectRevert(bytes calldata reason) external;
}

contract DeployTagtagSouvenirTest {
    DeployVm private constant VM =
        DeployVm(address(uint160(uint256(keccak256("hevm cheat code")))));

    function testDeploymentRejectsNonSepoliaChain() public {
        DeployTagtagSouvenir deployer = new DeployTagtagSouvenir();
        VM.expectRevert(abi.encodeWithSignature("Error(string)", "Sepolia only"));
        deployer.run();
    }
}
