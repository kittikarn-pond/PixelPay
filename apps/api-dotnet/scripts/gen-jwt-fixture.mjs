// Generates a Node-signed access token for .NET JWT parity tests.
// Usage: node scripts/gen-jwt-fixture.mjs
import { mkdirSync, writeFileSync } from 'fs';
import { createRequire } from 'module';
import { dirname } from 'path';
import { fileURLToPath } from 'url';

const require = createRequire(new URL('../../api/package.json', import.meta.url));
const jwt = require('jsonwebtoken');

const secret = 'test_jwt_secret_64_chars_minimum_for_testing_purposes_only';
const token = jwt.sign(
  { sub: '00000000-0000-0000-0000-000000000001', email: 'a@b.c', role: 'ADMIN' },
  secret,
  { expiresIn: '3650d' },
);

const out = fileURLToPath(new URL('../tests/PixelPay.UnitTests/Auth/fixtures/node-token.json', import.meta.url));
mkdirSync(dirname(out), { recursive: true });
writeFileSync(out, JSON.stringify({ secret, token }, null, 2) + '\n');
