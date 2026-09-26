import { createPublicClient, encodeFunctionData, getAddress, http, keccak256, parseAbi, TransactionReceiptNotFoundError } from 'viem';
import { sepolia } from 'viem/chains';
import { privateKeyToAccount } from 'viem/accounts';

const abi = parseAbi([
    'function mint(address recipient, uint256 tokenId, uint8 preset)',
    'function ownerOf(uint256 tokenId) view returns (address)',
    'function name() view returns (string)',
    'function symbol() view returns (string)'
]);

export function createMintChain(config, { publicClient = createPublicClient({ chain: sepolia, transport: http(config.rpcUrl, { timeout: 20000 }) }) } = {}) {
    if (config.chainId !== 11155111) throw new Error('Mint chain accepts Sepolia only');
    const contractAddress = getAddress(config.contractAddress);
    const account = privateKeyToAccount(config.privateKey);
    return {
        abi,
        signerAddress() { return account.address; },
        targetContractAddress() { return contractAddress; },
        async chainId() { return publicClient.getChainId(); },
        async pendingNonce() { return publicClient.getTransactionCount({ address: account.address, blockTag: 'pending' }); },
        async prepareMint({ recipient, tokenId, preset, nonce }) {
            const normalizedRecipient = getAddress(recipient);
            if (!Number.isInteger(preset) || preset < 0 || preset > 3) throw new Error('Invalid mint preset');
            const bytecode = await publicClient.getBytecode({ address: contractAddress });
            if (!bytecode || bytecode === '0x') throw Object.assign(new Error('Mint contract is not deployed'), { code: 'missing_contract' });
            const [name, symbol] = await Promise.all(['name', 'symbol'].map(functionName =>
                publicClient.readContract({ address: contractAddress, abi, functionName })));
            if (name !== 'tagtag discoveries' || symbol !== 'TAGTAG')
                throw Object.assign(new Error('Mint contract identity mismatch'), { code: 'wrong_contract' });
            const args = [normalizedRecipient, BigInt(tokenId), preset];
            const [gasEstimate, fees, balance] = await Promise.all([
                publicClient.estimateContractGas({ address: contractAddress, abi, functionName: 'mint', args, account: account.address }),
                publicClient.estimateFeesPerGas(),
                publicClient.getBalance({ address: account.address })
            ]);
            const gas = gasEstimate * 12n / 10n + 10000n;
            if (balance < gas * fees.maxFeePerGas) throw Object.assign(new Error('Insufficient signer balance'), { code: 'insufficient_funds' });
            const raw = await account.signTransaction({ type: 'eip1559', chainId: 11155111, nonce, to: contractAddress,
                data: encodeFunctionData({ abi, functionName: 'mint', args }), value: 0n, gas,
                maxFeePerGas: fees.maxFeePerGas, maxPriorityFeePerGas: fees.maxPriorityFeePerGas });
            return { raw, hash: keccak256(raw) };
        },
        async broadcast(raw) { return publicClient.sendRawTransaction({ serializedTransaction: raw }); },
        async receipt(hash) {
            try { return await publicClient.getTransactionReceipt({ hash }); }
            catch (error) { if (error instanceof TransactionReceiptNotFoundError) return null; throw error; }
        },
        async finalizedBlockNumber() { return (await publicClient.getBlock({ blockTag: 'finalized' })).number; },
        async blockHash(blockNumber) { return (await publicClient.getBlock({ blockNumber })).hash; },
        async tokenExists(tokenId) {
            try {
                await publicClient.readContract({ address: contractAddress, abi, functionName: 'ownerOf', args: [BigInt(tokenId)], blockTag: 'finalized' });
                return true;
            } catch (error) {
                if (error.name === 'ContractFunctionRevertedError' || error.cause?.name === 'ContractFunctionRevertedError') return false;
                throw error;
            }
        }
    };
}
