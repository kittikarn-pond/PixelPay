// eslint-disable-next-line @typescript-eslint/no-require-imports
const request = require('supertest');
import { createTarget, Target } from './support/target';

describe('Payments (e2e)', () => {
  let target: Target;
  let token: string;

  beforeAll(async () => {
    target = await createTarget();

    const res = await request(target.http)
      .post('/api/v1/auth/register')
      .send({
        email: `pay+${Date.now()}@test.com`,
        password: 'Test1234!',
        displayName: 'Payer',
      });
    token = res.body.data.accessToken;
  });

  afterAll(() => target.close());

  it('POST /payments/promptpay validates minimum amount', async () => {
    const res = await request(target.http)
      .post('/api/v1/payments/promptpay')
      .set('Authorization', `Bearer ${token}`)
      .send({ amount: 5 }) // below minimum 10
      .expect(400);
    expect(res.body.success).toBe(false);
  });

  it('GET /payments/:id returns 404 for unknown payment', async () => {
    await request(target.http)
      .get('/api/v1/payments/00000000-0000-0000-0000-000000000000')
      .set('Authorization', `Bearer ${token}`)
      .expect(404);
  });
});
