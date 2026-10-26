// eslint-disable-next-line @typescript-eslint/no-require-imports
const request = require('supertest');
import { createTarget, Target } from './support/target';

describe('Orders (e2e)', () => {
  let target: Target;
  let token: string;

  beforeAll(async () => {
    target = await createTarget();

    const res = await request(target.http)
      .post('/api/v1/auth/register')
      .send({
        email: `order+${Date.now()}@test.com`,
        password: 'Test1234!',
        displayName: 'Buyer',
      });
    token = res.body.data.accessToken;
  });

  afterAll(() => target.close());

  it('GET /orders returns empty list for new user', async () => {
    const res = await request(target.http)
      .get('/api/v1/orders')
      .set('Authorization', `Bearer ${token}`)
      .expect(200);
    expect(res.body.success).toBe(true);
    expect(res.body.data.items).toEqual([]);
    expect(res.body.data.total).toBe(0);
  });

  it('POST /orders returns 400 when product not found', async () => {
    const res = await request(target.http)
      .post('/api/v1/orders')
      .set('Authorization', `Bearer ${token}`)
      .send({
        gameProductId: '00000000-0000-0000-0000-000000000000',
        paymentMethod: 'WALLET',
        gameUid: '12345678',
      })
      .expect(400);
    expect(res.body.success).toBe(false);
  });
});
