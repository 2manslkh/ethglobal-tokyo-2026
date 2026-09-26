import { getAddress } from 'viem';
import { privateKeyToAccount } from 'viem/accounts';

export function apiNftConfig(env) {
    if (env.NFT_ENABLED !== 'true') return { enabled: false, chainId: 11155111 };
    if (env.NFT_CHAIN_ID !== '11155111') throw new Error('NFT minting requires Sepolia chain ID 11155111');
    let contractAddress;
    try { contractAddress = getAddress(env.NFT_CONTRACT_ADDRESS); }
    catch { throw new Error('NFT_CONTRACT_ADDRESS is invalid'); }
    if (!env.NFT_WALLET_DOMAIN || !/^[a-z0-9.-]+$/i.test(env.NFT_WALLET_DOMAIN)) throw new Error('NFT_WALLET_DOMAIN is invalid');
    return { enabled: true, chainId: 11155111, contractAddress, domain: env.NFT_WALLET_DOMAIN };
}

export function mintConfig(env) {
    const apiConfig = apiNftConfig(env);
    if (!apiConfig.enabled) return apiConfig;
    let rpcUrl;
    try { rpcUrl = new URL(env.NFT_RPC_URL); }
    catch { throw new Error('NFT_RPC_URL is invalid'); }
    if (rpcUrl.protocol !== 'https:') throw new Error('NFT_RPC_URL must use HTTPS');
    if (rpcUrl.username || rpcUrl.password) throw new Error('NFT_RPC_URL must not contain credentials');
    try { privateKeyToAccount(env.NFT_SIGNER_PRIVATE_KEY); }
    catch { throw new Error('NFT signer private key is missing or invalid'); }
    return { ...apiConfig, rpcUrl: rpcUrl.href, privateKey: env.NFT_SIGNER_PRIVATE_KEY };
}
