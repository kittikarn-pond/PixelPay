// eslint-disable-next-line @typescript-eslint/no-require-imports
const request = require('supertest');
import { createTarget, Target } from './support/target';
import * as bcrypt from 'bcrypt';

describe('Admin (e2e)', () => {
  let target: Target;
  let adminToken: string;
  let userToken: string;

  beforeAll(async () => {
    target = await createTarget();
    const { prisma } = target;

    // Create admin user directly in DB
    const passwordHash = await bcrypt.hash('Admin1234!', 12);
    const { customAlphabet } = await import('nanoid');
    const nanoid = customAlphabet('ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789', 8);
    await prisma.user.upsert({
      where: { email: 'admin@pixelpay.test' },
      update: {},
      create: {
        email: 'admin@pixelpay.test',
        passwordHash,
        displayName: 'Admin',
        role: 'ADMIN',
        referralCode: nanoid(),
        wallet: { create: {} },
      },
    });

    const adminRes = await request(target.http)
      .post('/api/v1/auth/login')
      .send({ email: 'admin@pixelpay.test', password: 'Admin1234!' });
    adminToken = adminRes.body.data.accessToken;

    const userRes = await request(target.http)
      .post('/api/v1/auth/register')
      .send({ email: `regular+${Date.now()}@test.com`, password: 'Test1234!', displayName: 'Regular' });
    userToken = userRes.body.data.accessToken;
  });

  afterAll(() => target.close());

  it('GET /admin/stats returns dashboard data (admin)', async () => {
    const res = await request(target.http)
      .get('/api/v1/admin/stats')
      .set('Authorization', `Bearer ${adminToken}`)
      .expect(200);
    expect(res.body.data.totalUsers).toBeDefined();
    expect(res.body.data.totalOrders).toBeDefined();
  });

  it('GET /admin/stats returns 403 for regular user', async () => {
    await request(target.http)
      .get('/api/v1/admin/stats')
      .set('Authorization', `Bearer ${userToken}`)
      .expect(403);
  });

  it('GET /admin/users returns paginated users', async () => {
    const res = await request(target.http)
      .get('/api/v1/admin/users')
      .set('Authorization', `Bearer ${adminToken}`)
      .expect(200);
    expect(res.body.data.items.length).toBeGreaterThan(0);
    expect(res.body.data.items[0].passwordHash).toBeUndefined();
  });
});
