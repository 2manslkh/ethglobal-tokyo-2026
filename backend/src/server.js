import { createServer } from 'node:http';
import { createApi } from './api.js';
import { createFirebaseAdapter } from './firebase-adapter.js';
import { apiNftConfig } from './nft-config.js';

const adapter = createFirebaseAdapter();
const port = Number(process.env.PORT || 8080);
if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error('Invalid PORT');
createServer(createApi({ adapter, nft: apiNftConfig(process.env) })).listen(port, '0.0.0.0');
