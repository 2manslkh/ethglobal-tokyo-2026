// SPDX-License-Identifier: MIT
pragma solidity ^0.8.24;

import {IAccessControl} from "@openzeppelin/contracts/access/IAccessControl.sol";
import {TagtagSouvenir} from "../src/TagtagSouvenir.sol";

interface Vm {
    function prank(address caller) external;
    function expectRevert() external;
    function expectRevert(bytes calldata reason) external;
}

contract TagtagSouvenirTest {
    Vm private constant VM = Vm(address(uint160(uint256(keccak256("hevm cheat code")))));
    address private constant ADMIN = address(0xA11CE);
    address private constant MINTER = address(0xB0B);
    address private constant HOLDER = address(0xCAFE);
    address private constant OTHER = address(0xBAD);

    TagtagSouvenir private souvenir;

    function setUp() public {
        souvenir = new TagtagSouvenir(ADMIN, MINTER, _uris());
    }

    function testMinterCreatesTokenWithSelectedPermanentMetadata() public {
        VM.prank(MINTER);
        souvenir.mint(HOLDER, 918, 2);

        require(souvenir.ownerOf(918) == HOLDER, "wrong recipient");
        require(keccak256(bytes(souvenir.tokenURI(918))) == keccak256("https://tagtag-nft-staging-2026.web.app/nft/v1/taggi-3.json"), "wrong URI");
        require(souvenir.presetOf(918) == 2, "wrong preset");

        VM.prank(HOLDER);
        souvenir.transferFrom(HOLDER, OTHER, 918);
        require(souvenir.ownerOf(918) == OTHER, "transfer failed");
        require(
            keccak256(bytes(souvenir.tokenURI(918))) == keccak256("https://tagtag-nft-staging-2026.web.app/nft/v1/taggi-3.json"), "URI changed"
        );
    }

    function testNonMinterCannotMint() public {
        VM.expectRevert(
            abi.encodeWithSelector(
                IAccessControl.AccessControlUnauthorizedAccount.selector,
                OTHER,
                souvenir.MINTER_ROLE()
            )
        );
        VM.prank(OTHER);
        souvenir.mint(HOLDER, 1, 0);
    }

    function testTokenIdCannotBeMintedAgainAfterTransfer() public {
        VM.prank(MINTER);
        souvenir.mint(HOLDER, 1, 0);
        VM.prank(HOLDER);
        souvenir.transferFrom(HOLDER, OTHER, 1);

        VM.expectRevert();
        VM.prank(MINTER);
        souvenir.mint(HOLDER, 1, 3);
        require(souvenir.ownerOf(1) == OTHER, "owner changed");
    }

    function testRejectsZeroRecipientAndOutOfRangePreset() public {
        VM.expectRevert();
        VM.prank(MINTER);
        souvenir.mint(address(0), 1, 0);

        VM.expectRevert();
        VM.prank(MINTER);
        souvenir.mint(HOLDER, 1, 4);
    }

    function testAdminControlsMinterRole() public {
        bytes32 minterRole = souvenir.MINTER_ROLE();
        require(souvenir.hasRole(souvenir.DEFAULT_ADMIN_ROLE(), ADMIN), "admin missing");
        require(souvenir.hasRole(minterRole, MINTER), "minter missing");
        require(!souvenir.hasRole(souvenir.DEFAULT_ADMIN_ROLE(), MINTER), "minter is admin");

        VM.expectRevert();
        VM.prank(OTHER);
        souvenir.grantRole(minterRole, OTHER);

        VM.prank(ADMIN);
        souvenir.grantRole(minterRole, OTHER);
        VM.prank(OTHER);
        souvenir.mint(HOLDER, 1, 0);

        VM.prank(ADMIN);
        souvenir.revokeRole(minterRole, OTHER);
        VM.expectRevert();
        VM.prank(OTHER);
        souvenir.mint(HOLDER, 2, 0);
    }

    function testAdminPausesMintWithoutPausingTransfers() public {
        VM.prank(MINTER);
        souvenir.mint(HOLDER, 1, 0);

        VM.expectRevert();
        VM.prank(OTHER);
        souvenir.setMintPaused(true);

        VM.prank(ADMIN);
        souvenir.setMintPaused(true);
        VM.expectRevert();
        VM.prank(MINTER);
        souvenir.mint(HOLDER, 2, 0);

        VM.prank(HOLDER);
        souvenir.transferFrom(HOLDER, OTHER, 1);
        require(souvenir.ownerOf(1) == OTHER, "transfer paused");

        VM.prank(ADMIN);
        souvenir.setMintPaused(false);
        VM.prank(MINTER);
        souvenir.mint(HOLDER, 2, 0);
    }

    function testRejectsInvalidDeploymentConfiguration() public {
        VM.expectRevert();
        new TagtagSouvenir(address(0), MINTER, _uris());
        VM.expectRevert();
        new TagtagSouvenir(ADMIN, address(0), _uris());

        string[4] memory uris = _uris();
        uris[1] = "";
        VM.expectRevert();
        new TagtagSouvenir(ADMIN, MINTER, uris);
    }

    function testUnknownTokenHasNoMetadata() public {
        VM.expectRevert();
        souvenir.tokenURI(999);
    }

    function _uris() private pure returns (string[4] memory uris) {
        uris[0] = "https://tagtag-nft-staging-2026.web.app/nft/v1/taggi-1.json";
        uris[1] = "https://tagtag-nft-staging-2026.web.app/nft/v1/taggi-2.json";
        uris[2] = "https://tagtag-nft-staging-2026.web.app/nft/v1/taggi-3.json";
        uris[3] = "https://tagtag-nft-staging-2026.web.app/nft/v1/taggi-4.json";
    }
}
