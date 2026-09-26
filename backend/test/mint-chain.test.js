import test from 'node:test';
import assert from 'node:assert/strict';
import { decodeFunctionData, parseTransaction } from 'viem';
import { createMintChain } from '../src/mint-chain.js';
import { apiNftConfig, mintConfig } from '../src/nft-config.js';

const contractAddress = '0x1234567890123456789012345678901234567890';
const recipient = '0x1111111111111111111111111111111111111111';
const privateKey = `0x${'1'.repeat(64)}`;

test('configuration rejects mainnet, missing signer and insecure RPC', () => {
    const valid = { NFT_ENABLED: 'true', NFT_CHAIN_ID: '11155111', NFT_CONTRACT_ADDRESS: contractAddress,
        NFT_RPC_URL: 'https://sepolia.example', NFT_SIGNER_PRIVATE_KEY: privateKey, NFT_WALLET_DOMAIN: 'tagtag.example' };
    assert.equal(mintConfig(valid).enabled, true);
    assert.equal(apiNftConfig({ NFT_ENABLED: 'true', NFT_CHAIN_ID: '11155111', NFT_CONTRACT_ADDRESS: contractAddress,
        NFT_WALLET_DOMAIN: 'tagtag.example' }).enabled, true);
    assert.throws(() => mintConfig({ ...valid, NFT_CHAIN_ID: '1' }), /Sepolia/);
    assert.throws(() => mintConfig({ ...valid, NFT_RPC_URL: 'http://mainnet.example' }), /HTTPS/);
    assert.throws(() => mintConfig({ ...valid, NFT_SIGNER_PRIVATE_KEY: '' }), /signer/i);
    assert.equal(mintConfig({}).enabled, false);
});

test('viem adapter signs exact mint calldata and sends serialized raw transaction', async () => {
    let broadcast = '';
    const publicClient = {
        async getChainId() { return 11155111; }, async getTransactionCount() { return 7; },
        async getBytecode() { return '0x6000'; },
        async readContract({ functionName }) { return functionName === 'name' ? 'tagtag discoveries' : 'TAGTAG'; },
        async estimateContractGas() { return 100000n; },
        async estimateFeesPerGas() { return { maxFeePerGas: 20n, maxPriorityFeePerGas: 2n }; },
        async getBalance() { return 3_000_000n; },
        async sendRawTransaction({ serializedTransaction }) { broadcast = serializedTransaction; }
    };
    const chain = createMintChain({ contractAddress, privateKey, rpcUrl: 'https://sepolia.example', chainId: 11155111 }, { publicClient });
    const signed = await chain.prepareMint({ recipient, tokenId: 123n, preset: 2, nonce: 7 });
    const parsed = parseTransaction(signed.raw);
    assert.equal(parsed.chainId, 11155111);
    assert.equal(parsed.nonce, 7);
    assert.equal(parsed.to.toLowerCase(), contractAddress.toLowerCase());
    assert.deepEqual(decodeFunctionData({ abi: chain.abi, data: parsed.data }).args, [recipient, 123n, 2]);
    assert.equal(signed.raw.includes('Private note'), false);
    await chain.broadcast(signed.raw);
    assert.equal(broadcast, signed.raw);
});

test('viem adapter refuses signing when balance cannot cover max gas fee', async () => {
    const publicClient = { async estimateContractGas() { return 100000n; },
        async getBytecode() { return '0x6000'; },
        async readContract({ functionName }) { return functionName === 'name' ? 'tagtag discoveries' : 'TAGTAG'; },
        async estimateFeesPerGas() { return { maxFeePerGas: 20n, maxPriorityFeePerGas: 2n }; },
        async getBalance() { return 1n; } };
    const chain = createMintChain({ contractAddress, privateKey, rpcUrl: 'https://sepolia.example', chainId: 11155111 }, { publicClient });
    await assert.rejects(chain.prepareMint({ recipient, tokenId: 123n, preset: 2, nonce: 7 }), error => error.code === 'insufficient_funds');
});

test('viem adapter refuses a deployed contract with the wrong NFT identity', async () => {
    const publicClient = { async getBytecode() { return '0x6000'; }, async readContract() { return 'other'; } };
    const chain = createMintChain({ contractAddress, privateKey, rpcUrl: 'https://sepolia.example', chainId: 11155111 }, { publicClient });
    await assert.rejects(chain.prepareMint({ recipient, tokenId: 123n, preset: 2, nonce: 7 }),
        error => error.code === 'wrong_contract');
});

test('viem adapter refuses to sign to an address with no deployed contract', async () => {
    const publicClient = { async getBytecode() { return undefined; } };
    const chain = createMintChain({ contractAddress, privateKey, rpcUrl: 'https://sepolia.example', chainId: 11155111 }, { publicClient });
    await assert.rejects(chain.prepareMint({ recipient, tokenId: 123n, preset: 2, nonce: 7 }),
        error => error.code === 'missing_contract');
});
