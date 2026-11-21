// eslint-disable-next-line @typescript-eslint/no-require-imports
const request = require('supertest');
import { createTarget, Target } from './support/target';

describe('Users (e2e)', () => {
  let target: Target;
  let accessToken: string;

  beforeAll(async () => {
    target = await createTarget();

    const res = await request(target.http)
      .post('/api/v1/auth/register')
      .send({
        email: `user+${Date.now()}@test.com`,
        password: 'Test1234!',
        displayName: 'User',
      });
    accessToken = res.body.data.accessToken;
  });

  afterAll(() => target.close());

  it('GET /api/v1/users/profile returns profile', async () => {
    const res = await request(target.http)
      .get('/api/v1/users/profile')
      .set('Authorization', `Bearer ${accessToken}`)
      .expect(200);
    expect(res.body.data.email).toBeDefined();
  });

  it('PATCH /api/v1/users/profile updates display name', async () => {
    const res = await request(target.http)
      .patch('/api/v1/users/profile')
      .set('Authorization', `Bearer ${accessToken}`)
      .send({ displayName: 'Updated Name' })
      .expect(200);
    expect(res.body.data.displayName).toBe('Updated Name');
  });
});
