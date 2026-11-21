# PixelPay Backend Migration: NestJS → .NET 10 — Design

Date: 2026-10-26. Decisions settled in a grilling session (Q1–Q47).

## Why

The team works best in C#. The rewrite is a full replacement of `apps/api` (NestJS 10 + Fastify + Prisma + BullMQ + Socket.io). The frontend (`apps/web`, Next.js) keeps working against the same HTTP contract.

## Current system (facts)

- `apps/api`: ~105 TS files, ~4.7k lines, 18 domain modules. Global prefix `api/v1`. ValidationPipe with `whitelist`, `forbidNonWhitelisted`, `transform`.
- Two processes from one codebase: HTTP (`src/main.ts`) and worker (`src/main.worker.ts`, BullMQ queues `topup`, payments, notifications).
- Prod (`docker-compose.prod.yml`): `api` ×2, `topup-worker` ×2, `notification-worker` ×1, MySQL 8, Redis 7.
- Prisma owns the MySQL schema (`apps/api/prisma/schema.prisma`, one baseline migration `20260915040045_init`).
- Auth: HS256 JWT access + refresh, payload `{ sub, email, role }`, secrets `JWT_SECRET` / `JWT_REFRESH_SECRET`, expiry `JWT_EXPIRES_IN` (`15m`) / `JWT_REFRESH_EXPIRES_IN` (`7d`). Stateless. bcrypt cost 12.
- Provider credentials are encrypted with AES-256-GCM, stored as `ivHex:tagHex:dataHex`, key = `ENCRYPTION_KEY` (hex).
- SmileOne / UniPin / GB Prime Pay are plain axios HTTP clients (HMAC-SHA256 signing for SmileOne/UniPin). No Node-only SDKs.
- GB Prime Pay webhook `POST /api/v1/payments/webhook` is unsigned by design. It reads `referenceNo`, re-queries GB Prime Pay's status API, and credits the wallet inside a transaction guarded by `status = PENDING`.
- Live order status goes through Socket.io namespace `/orders`. Its one consumer is `apps/web/components/orders/OrderTracker.tsx`.
- `helmet` is a dependency but is **not** applied in `main.ts`. CORS: origin `FRONTEND_URL`, `credentials: true`.
- Tests: 8 unit specs; 9 Jest e2e suites that boot Nest in-process via supertest.

## Response contract (must match exactly)

- Success: `{ "success": true, "data": <value or null> }`
- Error: `{ "success": false, "error": <Nest exception body> }` where the body is:
  - `{ "statusCode": 400, "message": ["..."], "error": "Bad Request" }` for validation (class-validator default message text)
  - `{ "statusCode": N, "message": "...", "error": "<Reason Phrase>" }` for `XxxException('msg')`
  - `{ "statusCode": 401, "message": "Unauthorized" }` for a failed JWT guard or a bare `UnauthorizedException()`
  - `{ "statusCode": 403, "message": "Forbidden resource", "error": "Forbidden" }` for a failed roles guard
  - `{ "message": "Internal server error" }` with HTTP 500 for unhandled errors
- JSON is camelCase. Null fields are present as `null`. Enums are their string names.
- Prisma `Decimal` serializes as a **string** with trailing zeros removed (`"100"`, `"12.5"`).
- Dates serialize as `YYYY-MM-DDTHH:mm:ss.sssZ` (UTC, always 3 fraction digits).
- POST without `@HttpCode` returns 201. `/auth/login` and `/auth/refresh` return 200.

## Decisions

| # | Decision |
|---|---|
| Q1, Q6, Q8 | Full rewrite in a new folder `apps/api-dotnet`. It runs side by side with Node until cutover. |
| Q3, Q14, Q22 | Frontend unchanged. Same routes, `api/v1` prefix, envelope, camelCase. One exception: Q44. |
| Q4, Q23 | Keep the MySQL schema. Prisma owns migrations until cutover. EF Core is scaffolded from the DB and creates no migrations before then. |
| Q5, Q24→Q35 | EF Core **9** + **Pomelo.EntityFrameworkCore.MySql 9.0.0** on the .NET 10 runtime. Pomelo has no EF Core 10 build yet. Move to EF 10 when Pomelo ships it. |
| Q7, Q12 | Built by the user and Claude, piece by piece, with no hard deadline. |
| Q9, Q21, Q26 | **Phase 1:** auth, users, wallet (incl. admin deposit), orders **read** (GET list/detail). **Phase 2:** order create/cancel, fulfillment engine, payments (QR create, webhook, poller), notifications, SignalR. Other modules (games, categories, coupons, admin, affiliates, resellers, cashback, audit, analytics) each get their own plan after Phase 2. |
| Q10, Q36 | Parity is proven by the existing Jest e2e suites, retargeted through `E2E_BASE_URL` and run against Node and then against .NET. |
| Q11, Q25, Q46 | Dev and prod run Node and .NET at the same time on one DB. A YARP gateway (`PixelPay.Gateway`) owns the public port and routes each path to Node or .NET. |
| Q13, Q45 | Clean Architecture: `PixelPay.Domain`, `PixelPay.Application`, `PixelPay.Infrastructure`, `PixelPay.Api`, `PixelPay.Worker`, `PixelPay.Gateway`, plus `tests/PixelPay.UnitTests` and `tests/PixelPay.IntegrationTests`. |
| Q15 | Swagger (Swashbuckle). |
| Q16, Q32 | FluentValidation. Errors are rendered in the Nest validation shape. Unknown body properties are rejected (`property x should not exist`). |
| Q17, Q41 | .NET is outside Turborepo. It has its own CI job (`dotnet build`, `dotnet test`, e2e against .NET). |
| Q18, Q28, Q33 | Hangfire on Redis (`Hangfire.Redis.StackExchange`). Queues `topup`, `payments`, `notifications`. One Worker image whose env selects the queues. Same 3-box prod shape. (Phase 2) |
| Q19, Q44 | SignalR replaces Socket.io. `OrderTracker.tsx` switches to `@microsoft/signalr` in the same release as the Phase 2 fulfillment move. (Phase 2) |
| Q20 | Tokens work on both sides: same secrets, same claims, HS256, zero clock skew. |
| Q27 | BCrypt.Net-Next, cost 12. Existing hashes are kept. |
| Q29 | xUnit + Testcontainers (real MySQL/Redis) + WebApplicationFactory. No DB mocks. |
| Q30, Q31 | The webhook is ported exactly (unsigned, re-query status, guarded credit) and moves with the whole payments piece in Phase 2. |
| Q34 | `/api/v1/health`, `/health/db`, `/health/queues` keep their paths. `/health/queues` arrives with Hangfire in Phase 2. |
| Q37 | .NET reads and writes the same AES-256-GCM `iv:tag:data` hex format with the same `ENCRYPTION_KEY`. |
| Q38 | Same env names (`DATABASE_URL`, `REDIS_URL`, `JWT_SECRET`, `JWT_REFRESH_SECRET`, `JWT_EXPIRES_IN`, `JWT_REFRESH_EXPIRES_IN`, `ENCRYPTION_KEY`, `APP_PORT`, `FRONTEND_URL`). `DATABASE_URL` (`mysql://…`) is parsed into a connection string. |
| Q39 | MailKit and the Twilio .NET SDK, with the same template text. (Phase 2) |
| Q40 | Serilog, JSON to console. |
| Q42 | Kill Node when every gateway route points to .NET, e2e is green against .NET, and 2 weeks of prod pass with no major errors. Rollback means flipping a gateway route back to Node. After the kill, remove the gateway. |
| Q43 | Copy CORS (origin `FRONTEND_URL`, credentials). There are no helmet headers to copy because helmet is never applied. |
| Q47 | Kill day: `prisma/seed.ts` is ported to `dotnet run -- seed`. The first EF migration is a no-change baseline of the current schema. |

## Prisma behaviour .NET must reproduce on writes

- `@default(uuid())` is generated by the client. .NET generates `Guid.NewGuid().ToString()` (lowercase, 36 chars).
- `@updatedAt` is set by the client. .NET sets `updated_at = UTC now` on every update, including bulk `ExecuteUpdate`.
- All `DateTime(3)` values are UTC.
- Referral codes are 8 chars from `A–Z0–9`.

## Open items (outside this migration, noted only)

- `.github/workflows/ci.yml` triggers on `main`/`develop`, but the working branch is `master`.
- CI `ENCRYPTION_KEY` is 32 hex chars (16 bytes). AES-256 needs 32 bytes, so any test that touches encryption throws.

## Plans

- Phase 1: `docs/superpowers/plans/2026-10-26-dotnet-migration-phase1.md`
- Phase 2, per-module plans and cutover: written after Phase 1 lands.
