# PixelPay

Game top-up payment platform built with .NET 10, Next.js 14, and MySQL.

## Stack

| Layer | Technology |
|-------|-----------|
| API | ASP.NET Core (.NET 10), EF Core 9 + Pomelo MySQL, FluentValidation |
| Background jobs | Hangfire on Redis (`PixelPay.Worker`) |
| Live order status | SignalR (`/hubs/orders`), Redis backplane |
| Frontend | Next.js 14 (App Router), Tailwind CSS, Zustand |
| Database | MySQL 8 |
| Cache / Queue | Redis 7 |
| Reverse Proxy | Nginx (production) |

---

## Prerequisites

- .NET SDK 10
- Node.js >= 20 (frontend and e2e suites)
- Docker & Docker Compose

---

## Quick Start (Docker — recommended)

### 1. Create environment file

```bash
cp .env.example .env
```

Edit `.env` and set at minimum:

```env
JWT_SECRET=<random string, 32+ bytes>
JWT_REFRESH_SECRET=<random string, 32+ bytes>
ENCRYPTION_KEY=<exactly 64 hex chars>
```

### 2. Start the full stack

```bash
docker compose up --build
```

This starts:
- **MySQL** on port `3306` and **Redis** on port `6379`
- **migrate** — applies EF Core migrations, then exits
- **API** on `http://localhost:4000` (`dotnet watch`)
- **Worker** — Hangfire jobs: top-ups, PromptPay polling, notifications
- **Frontend** on `http://localhost:4001`

### 3. Seed the storefront catalog (optional, first time)

```bash
docker compose run --rm api dotnet run --project src/PixelPay.Api -- seed
```

### 4. Open the app

- Frontend: http://localhost:4001
- Swagger (Development): http://localhost:4000/swagger
- Health check: http://localhost:4000/api/v1/health

---

## Local Development (without Docker)

```bash
docker compose up mysql redis -d
cp .env.example .env        # DATABASE_URL / REDIS_URL point at localhost

cd apps/api-dotnet
dotnet run --project src/PixelPay.Api -- migrate
dotnet run --project src/PixelPay.Api -- seed      # optional
dotnet watch --project src/PixelPay.Api run        # API on APP_PORT (3000)
dotnet watch --project src/PixelPay.Worker run     # background jobs

cd ../web && npm install && npm run dev            # frontend on 3001
```

The .NET projects read configuration from environment variables (the same names as `.env`).

---

## Project Structure

```
pixelpay/
├── apps/
│   ├── api-dotnet/                 # .NET backend
│   │   ├── src/
│   │   │   ├── PixelPay.Domain/        # Entities, enums
│   │   │   ├── PixelPay.Application/   # Use cases: auth, wallet, orders, top-up job, payments,
│   │   │   │                           # catalog, promotions, notifications, admin, analytics
│   │   │   ├── PixelPay.Infrastructure/# EF Core + migrations, Hangfire, SignalR hub, GB Prime Pay,
│   │   │   │                           # SmileOne/UniPin, email/SMS, catalog seed
│   │   │   ├── PixelPay.Api/           # HTTP API (Nest-compatible envelope and errors)
│   │   │   └── PixelPay.Worker/        # Hangfire server (WORKER_QUEUES)
│   │   └── tests/                  # xUnit unit + integration (Testcontainers)
│   ├── e2e/                        # HTTP e2e suites (Jest) against a running API, k6 load test
│   └── web/                        # Next.js 14 frontend
├── infrastructure/
│   ├── docker/                     # Dockerfiles (api, worker, web)
│   ├── nginx/                      # Nginx config (TLS, rate limiting, /hubs WebSockets)
│   └── scripts/                    # db-backup.sh
├── .github/workflows/              # CI (tests) + CD (GHCR push, SSH deploy)
├── docker-compose.yml              # Development stack
└── docker-compose.prod.yml         # Production stack
```

---

## Commands

From `apps/api-dotnet`:

| Command | Description |
|---------|-------------|
| `dotnet test` | Unit and integration tests (needs Docker for Testcontainers) |
| `dotnet run --project src/PixelPay.Api -- migrate` | Apply pending EF Core migrations |
| `dotnet run --project src/PixelPay.Api -- seed` | Upsert the storefront catalog |
| `dotnet ef migrations add <Name> --project src/PixelPay.Infrastructure --output-dir Persistence/Migrations` | Create a migration (`dotnet tool restore` first) |

From `apps/e2e` (with the API running and migrated):

```bash
E2E_BASE_URL=http://localhost:4000 DATABASE_URL=mysql://pixelpay:pixelpay@localhost:3306/pixelpay npm run test:e2e
```

### Database migrations

EF Core owns the schema. `InitialCreate` is the schema Prisma used to create, statement for statement. On a
database Prisma created, `migrate` records `InitialCreate` as applied instead of running it, so existing data is
untouched. Migrations take a MySQL named lock, so concurrent runs are safe.

---

## API Overview

Base URL: `/api/v1`. Responses are `{ "success": true, "data": ... }` or `{ "success": false, "error": ... }`.

| Group | Endpoints |
|-------|-----------|
| Auth | `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh`, `GET /auth/me`, `POST /auth/logout` |
| Users | `GET /users/profile`, `PATCH /users/profile`, `PUT /users/change-password` |
| Wallet | `GET /wallet`, `GET /wallet/transactions`, `POST /wallet/deposit` (admin) |
| Games | `GET /games`, `GET /games/:slug`, `GET /games/:slug/products`, `GET /categories` |
| Orders | `POST /orders`, `GET /orders`, `GET /orders/:id`, `POST /orders/:id/cancel` |
| Payments | `POST /payments/promptpay`, `GET /payments/:id`, `POST /payments/webhook` |
| Coupons | `POST /coupons/validate` |
| Affiliates | `GET /affiliates/dashboard`, `GET /affiliates/commissions` |
| Notifications | `GET /notifications`, `PATCH /notifications/read-all`, `PATCH /notifications/:id/read` |
| Resellers | `POST /reseller/apply`, `GET /reseller/products` |
| Admin | stats, users, orders (status override, retry), providers, games, products, categories, resellers, analytics |
| Health | `GET /health`, `GET /health/db`, `GET /health/queues` |

Live order status: SignalR hub `/hubs/orders` (access token in the `access_token` query string); the server sends
`order.status` with `{ orderId, status }` to the buyer.

---

## Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `DATABASE_URL` | MySQL connection string (`mysql://user:pass@host:port/db`) | Yes |
| `REDIS_URL` | Redis connection string (Hangfire storage, SignalR backplane) | Yes |
| `JWT_SECRET` | JWT signing secret (32+ bytes) | Yes |
| `JWT_REFRESH_SECRET` | Refresh token secret (32+ bytes) | Yes |
| `ENCRYPTION_KEY` | AES-256 key for provider credentials (64 hex chars) | Yes |
| `APP_PORT` | API port (default 3000) | Optional |
| `FRONTEND_URL` | Allowed CORS origin | Yes |
| `PAYMENT_GATEWAY_URL` | GB Prime Pay API base URL — confirm with your account rep | Yes |
| `PAYMENT_GATEWAY_API_KEY` | GB Prime Pay "token" (Profile > Gen Token); requires QR Cash enabled on the account | Yes |
| `PAYMENT_GATEWAY_SECRET` | GB Prime Pay merchant secret key, for the status-query API | Yes |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASS`, `SMTP_FROM` | Email notifications | Optional |
| `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN`, `TWILIO_FROM` | SMS notifications | Optional |
| `WORKER_QUEUES` | Worker queues: `topup`, `payments`, `notifications` (default all) | Optional |
| `NEXT_PUBLIC_API_URL` | API URL for the frontend | Yes |
| `NEXT_PUBLIC_WS_URL` | API base URL for the SignalR hub | Yes |

---

## Production Deployment

### Docker (self-hosted VPS)

```bash
# On the server
cp .env.example .env
# Fill in production values

docker compose -f docker-compose.prod.yml up --build -d
```

The `migrate` service runs first and the API and workers wait for it. The first deploy of the .NET backend
baselines the existing Prisma-created database (no schema changes).

Requires TLS certificates in `/etc/letsencrypt` (Let's Encrypt / Certbot).

### CI/CD (GitHub Actions)

- CI (push to `master`/`develop`, PRs to `master`): .NET build + unit/integration tests, e2e suites against a
  running API and worker, frontend build.
- CD (`deploy.yml`): builds the api, worker and web images, pushes to GHCR, deploys to the VPS via SSH.
  It triggers on pushes to `main`, which this repository does not have, so it does not run today.

Required GitHub secrets: `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY`

---

## Load Testing

Requires [k6](https://k6.io/docs/get-started/installation/).

```bash
k6 run apps/e2e/load/topup-flow.js

# Against a specific environment
API_URL=https://api.yourdomain.com/api/v1 k6 run apps/e2e/load/topup-flow.js
```

Ramps to 20 virtual users over 30 s, holds for 1 min, ramps down.
Thresholds: p95 < 2 s, error rate < 1%.

---

## Database Backups

```bash
# Manual backup to S3
bash infrastructure/scripts/db-backup.sh

# Required env vars
S3_BUCKET=pixelpay
S3_ENDPOINT=https://s3.amazonaws.com
S3_ACCESS_KEY=...
S3_SECRET_KEY=...
MYSQL_PWD=...
DB_NAME=pixelpay
DB_USER=pixelpay
```
