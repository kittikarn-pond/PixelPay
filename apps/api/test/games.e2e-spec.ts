// eslint-disable-next-line @typescript-eslint/no-require-imports
const request = require('supertest');
import { createTarget, Target } from './support/target';

describe('Games (e2e)', () => {
  let target: Target;

  beforeAll(async () => {
    target = await createTarget();
  });

  afterAll(() => target.close());

  it('GET /api/v1/games returns array', async () => {
    const res = await request(target.http)
      .get('/api/v1/games')
      .expect(200);
    expect(res.body.success).toBe(true);
    expect(Array.isArray(res.body.data)).toBe(true);
  });

  it('GET /api/v1/games/:slug returns 404 for unknown slug', async () => {
    await request(target.http)
      .get('/api/v1/games/unknown-game-xyz')
      .expect(404);
  });
});
