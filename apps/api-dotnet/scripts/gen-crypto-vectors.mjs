// Generates Node-produced crypto vectors for .NET parity tests.
// Usage: node scripts/gen-crypto-vectors.mjs > tests/PixelPay.UnitTests/Security/fixtures/node-vectors.json
import * as crypto from 'crypto';

const keyHex = crypto.randomBytes(32).toString('hex');
const plaintext = 'pixelpay-secret ทดสอบ 🔑';
const hmacSecret = 'webhook-secret';
const hmacData = '{"orderId":"123","amount":"99.50"}';

const iv = crypto.randomBytes(12);
const cipher = crypto.createCipheriv('aes-256-gcm', Buffer.from(keyHex, 'hex'), iv);
const encrypted = Buffer.concat([cipher.update(plaintext, 'utf8'), cipher.final()]);
const ciphertext = `${iv.toString('hex')}:${cipher.getAuthTag().toString('hex')}:${encrypted.toString('hex')}`;

const hmacHex = crypto.createHmac('sha256', hmacSecret).update(hmacData).digest('hex');

console.log(JSON.stringify({ keyHex, plaintext, ciphertext, hmacSecret, hmacData, hmacHex }, null, 2));
