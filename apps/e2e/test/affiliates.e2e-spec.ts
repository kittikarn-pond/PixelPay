// eslint-disable-next-line @typescript-eslint/no-require-imports
const request = require('supertest');
import { createTarget, Target } from './support/target';

describe('Affiliates (e2e)', () => {
  let target: Target;
  let token: string;

  beforeAll(async () => {
    target = await createTarget();

    const res = await request(target.http)
      .post('/api/v1/auth/register')
      .send({ email: `aff+${Date.now()}@test.com`, password: 'Test1234!', displayName: 'Affiliate' });
    token = res.body.data.accessToken;
  });

  afterAll(() => target.close());

  it('GET /affiliates/dashboard returns stats', async () => {
    const res = await request(target.http)
      .get('/api/v1/affiliates/dashboard')
      .set('Authorization', `Bearer ${token}`)
      .expect(200);

    expect(res.body.success).toBe(true);
    expect(res.body.data.totalReferrals).toBe(0);
    expect(res.body.data.totalEarnings).toBeDefined();
    expect(res.body.data.pendingEarnings).toBeDefined();
  });

  it('GET /affiliates/commissions returns paginated list', async () => {
    const res = await request(target.http)
      .get('/api/v1/affiliates/commissions')
      .set('Authorization', `Bearer ${token}`)
      .expect(200);

    expect(res.body.success).toBe(true);
    expect(res.body.data.items).toEqual([]);
    expect(res.body.data.total).toBe(0);
  });
});
