// eslint-disable-next-line @typescript-eslint/no-require-imports
const request = require('supertest');
import { createTarget, Target } from './support/target';
import bcrypt from 'bcryptjs';

describe('Wallet (e2e)', () => {
  let target: Target;
  let userToken: string;
  let adminToken: string;
  let targetUserId: string;

  beforeAll(async () => {
    target = await createTarget();

    // Register a regular user via API
    const ts = Date.now();
    const userRes = await request(target.http)
      .post('/api/v1/auth/register')
      .send({
        email: `wallet-user+${ts}@test.com`,
        password: 'Test1234!',
        displayName: 'Wallet User',
      });
    userToken = userRes.body.data.accessToken;
    targetUserId = userRes.body.data.user.id;

    // Create admin user directly in DB (auth register always creates USER role)
    const passwordHash = await bcrypt.hash('Admin1234!', 12);
    const adminUser = await target.db.createUser({
      email: `wallet-admin+${ts}@test.com`,
      passwordHash,
      displayName: 'Wallet Admin',
      referralCode: `ADMW${ts.toString().slice(-8)}`,
      role: 'ADMIN',
    });

    // Login as admin
    const adminRes = await request(target.http)
      .post('/api/v1/auth/login')
      .send({ email: adminUser.email, password: 'Admin1234!' });
    adminToken = adminRes.body.data.accessToken;
  });

  afterAll(() => target.close());

  describe('GET /api/v1/wallet', () => {
    it('returns wallet data for authenticated user', async () => {
      const res = await request(target.http)
        .get('/api/v1/wallet')
        .set('Authorization', `Bearer ${userToken}`)
        .expect(200);

      expect(res.body.success).toBe(true);
      expect(res.body.data.balance).toBeDefined();
      expect(res.body.data.lockedBalance).toBeDefined();
      // New user wallet starts at 0
      expect(parseFloat(res.body.data.balance)).toBe(0);
      expect(parseFloat(res.body.data.lockedBalance)).toBe(0);
    });

    it('returns 401 for unauthenticated request', async () => {
      await request(target.http)
        .get('/api/v1/wallet')
        .expect(401);
    });
  });

  describe('GET /api/v1/wallet/transactions', () => {
    it('returns paginated transactions for authenticated user', async () => {
      const res = await request(target.http)
        .get('/api/v1/wallet/transactions')
        .set('Authorization', `Bearer ${userToken}`)
        .expect(200);

      expect(res.body.success).toBe(true);
      expect(res.body.data.items).toEqual([]);
      expect(res.body.data.total).toBe(0);
    });

    it('returns 401 for unauthenticated request', async () => {
      await request(target.http)
        .get('/api/v1/wallet/transactions')
        .expect(401);
    });
  });

  describe('POST /api/v1/wallet/deposit', () => {
    it('admin can deposit to a user wallet', async () => {
      const res = await request(target.http)
        .post('/api/v1/wallet/deposit')
        .set('Authorization', `Bearer ${adminToken}`)
        .send({ userId: targetUserId, amount: 100, description: 'Test deposit' })
        .expect(201);

      expect(res.body.success).toBe(true);
      expect(res.body.data).toBeDefined();
    });

    it('wallet balance increases after deposit', async () => {
      const res = await request(target.http)
        .get('/api/v1/wallet')
        .set('Authorization', `Bearer ${userToken}`)
        .expect(200);

      expect(parseFloat(res.body.data.balance)).toBe(100);
    });

    it('regular user cannot deposit (403)', async () => {
      await request(target.http)
        .post('/api/v1/wallet/deposit')
        .set('Authorization', `Bearer ${userToken}`)
        .send({ userId: targetUserId, amount: 50 })
        .expect(403);
    });

    it('unauthenticated deposit returns 401', async () => {
      await request(target.http)
        .post('/api/v1/wallet/deposit')
        .send({ userId: targetUserId, amount: 50 })
        .expect(401);
    });

    it('deposit with invalid amount returns 400', async () => {
      await request(target.http)
        .post('/api/v1/wallet/deposit')
        .set('Authorization', `Bearer ${adminToken}`)
        .send({ userId: targetUserId, amount: -10 })
        .expect(400);
    });

    it('deposit with missing userId returns 400', async () => {
      await request(target.http)
        .post('/api/v1/wallet/deposit')
        .set('Authorization', `Bearer ${adminToken}`)
        .send({ amount: 50 })
        .expect(400);
    });
  });
});
