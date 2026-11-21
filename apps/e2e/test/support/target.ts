import { randomUUID, randomInt } from 'crypto';
import mysql from 'mysql2/promise';

/**
 * The API under test is always a running server: E2E_BASE_URL (e.g. http://localhost:3000).
 * DATABASE_URL points at its database, for the few fixtures the API cannot create (admins, coupons).
 */
export interface Target {
  http: string;
  db: TestDb;
  close(): Promise<void>;
}

export async function createTarget(): Promise<Target> {
  const http = process.env.E2E_BASE_URL;
  if (!http) throw new Error('Set E2E_BASE_URL to the running API, e.g. http://localhost:3000');
  const url = process.env.DATABASE_URL;
  if (!url) throw new Error('Set DATABASE_URL to the API database, e.g. mysql://pixelpay:pixelpay@localhost:3306/pixelpay');
  const pool = mysql.createPool({ uri: url, connectionLimit: 2 });
  const db = new TestDb(pool);
  return { http, db, close: () => pool.end() };
}

const ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';
const code = (n: number) => Array.from({ length: n }, () => ALPHABET[randomInt(ALPHABET.length)]).join('');

export interface NewUser {
  email: string;
  passwordHash: string;
  displayName: string;
  role?: 'USER' | 'ADMIN' | 'RESELLER';
  referralCode?: string;
}

export interface NewCoupon {
  code: string;
  discountType: 'PERCENTAGE' | 'FIXED';
  value: number;
  minOrderAmount?: number;
  usageLimit?: number | null;
  userLimit?: number;
  isActive?: boolean;
}

export class TestDb {
  constructor(private readonly pool: mysql.Pool) {}

  /** A user plus their wallet, as registration would create them. */
  async createUser(u: NewUser): Promise<{ id: string; email: string }> {
    const id = randomUUID();
    const conn = await this.pool.getConnection();
    try {
      await conn.beginTransaction();
      await conn.execute(
        `INSERT INTO users (id, email, password_hash, display_name, role, referral_code, updated_at)
         VALUES (?, ?, ?, ?, ?, ?, NOW(3))`,
        [id, u.email, u.passwordHash, u.displayName, u.role ?? 'USER', u.referralCode ?? code(8)],
      );
      await conn.execute(
        `INSERT INTO wallets (id, user_id, balance, locked_balance, currency, updated_at)
         VALUES (?, ?, 0, 0, 'THB', NOW(3))`,
        [randomUUID(), id],
      );
      await conn.commit();
      return { id, email: u.email };
    } catch (e) {
      await conn.rollback();
      throw e;
    } finally {
      conn.release();
    }
  }

  /** Creates the user only if no user has this email yet. */
  async ensureUser(u: NewUser): Promise<{ id: string; email: string }> {
    const [rows] = await this.pool.execute<mysql.RowDataPacket[]>('SELECT id, email FROM users WHERE email = ?', [u.email]);
    if (rows.length > 0) return { id: rows[0].id, email: rows[0].email };
    return this.createUser(u);
  }

  async createCoupon(c: NewCoupon): Promise<{ id: string; code: string }> {
    const id = randomUUID();
    await this.pool.execute(
      `INSERT INTO coupons (id, code, discount_type, value, min_order_amount, usage_limit, user_limit, is_active)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?)`,
      [id, c.code, c.discountType, c.value, c.minOrderAmount ?? 0, c.usageLimit ?? null, c.userLimit ?? 1, c.isActive ?? true],
    );
    return { id, code: c.code };
  }

  async findCouponByCodePrefix(prefix: string): Promise<{ id: string; code: string } | null> {
    const [rows] = await this.pool.execute<mysql.RowDataPacket[]>(
      'SELECT id, code FROM coupons WHERE code LIKE ? ORDER BY created_at DESC LIMIT 1',
      [`${prefix}%`],
    );
    return rows.length > 0 ? { id: rows[0].id, code: rows[0].code } : null;
  }
}
