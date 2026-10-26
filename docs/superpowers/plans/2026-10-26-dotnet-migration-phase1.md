# .NET Migration Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Serve auth, users, wallet and order-read endpoints from a new .NET 10 API behind a YARP gateway. Everything else stays on Node, and the frontend sees no difference.

**Architecture:** `apps/api-dotnet` is a Clean Architecture solution. It reads the Prisma-owned MySQL schema through a scaffolded EF Core 9 context. A YARP gateway takes the public port and routes Phase 1 paths to .NET and the rest to Node. Parity is proven by running the existing Jest e2e suites against both backends through `E2E_BASE_URL`.

**Tech Stack:** .NET 10 SDK, ASP.NET Core controllers, EF Core 9.0.x + Pomelo.EntityFrameworkCore.MySql 9.0.0, FluentValidation 11, Microsoft.AspNetCore.Authentication.JwtBearer 10, BCrypt.Net-Next 4, Serilog.AspNetCore, Swashbuckle.AspNetCore, Yarp.ReverseProxy 2, xUnit, Testcontainers.MySql, Microsoft.AspNetCore.Mvc.Testing 10.

**Spec:** `docs/superpowers/specs/2026-10-26-dotnet-backend-migration-design.md`

## Global Constraints

- `TargetFramework` `net10.0`; `global.json` sdk `10.0.100`, `rollForward: latestFeature`.
- EF Core packages pinned to `9.0.*` (Pomelo 9.0.0 does not accept EF 10). Package versions live in `apps/api-dotnet/Directory.Packages.props` (central package management).
- No EF migrations in Phase 1. Prisma owns the schema.
- Every route sits under `api/v1`.
- Response envelope, error bodies, Decimal-as-string, date format and status codes are exactly as in the spec's "Response contract" section.
- Env names: `DATABASE_URL`, `JWT_SECRET`, `JWT_REFRESH_SECRET`, `JWT_EXPIRES_IN` (default `15m`), `JWT_REFRESH_EXPIRES_IN` (default `7d`), `ENCRYPTION_KEY`, `APP_PORT` (default `3000`), `FRONTEND_URL` (default `http://localhost:3001`).
- JWT: HS256, claims `sub`, `email`, `role`, `iat`, `exp`. Inbound claim mapping off. `ClockSkew = TimeSpan.Zero`.
- Writes generate lowercase GUID ids and set `updated_at` to UTC now (Prisma did both client-side).
- No FluentAssertions (license). Use xUnit `Assert`.
- Do not modify any Node file except the e2e test harness (Task 1) and `docker-compose.yml` (Task 11).

## Review Focus

- Money fields are JSON **strings** without trailing zeros (`"balance":"100"`). A .NET number `100.00` would silently change frontend formatting. Pinned in Task 3 and Task 9.
- Unknown body properties return 400 `property foo should not exist`. ASP.NET ignores them by default. Pinned in Task 3 and Task 7.
- A token expired by 1 second must be rejected (.NET's default skew is 5 min). Pinned in Task 6.
- A token minted by Node must validate in .NET, and the `role` claim must not be remapped to the `ClaimTypes.Role` URI. Pinned in Task 6 (fixture) and Task 11 (live cross-login).
- A concurrent admin deposit and Node worker lock on the same wallet must not lose an update. Use a SQL-side increment, never a tracked-entity read-modify-write. Pinned in Task 9.

---

### Task 1: Retarget Node e2e suites via `E2E_BASE_URL`

**Files:**
- Create: `apps/api/test/support/target.ts`
- Modify: all 9 `apps/api/test/*.e2e-spec.ts`

**Interfaces:**
- Produces: `export async function createTarget(): Promise<{ http: string | import('http').Server; prisma: PrismaClient; close(): Promise<void> }>`. When `E2E_BASE_URL` is set, `http` is that URL and `prisma` is a plain `new PrismaClient()`. Otherwise it boots Nest in-process exactly as the suites do today and returns `app.getHttpServer()` and `app.get(PrismaService)`.

- [ ] **Step 0:** Run `cd apps/api && npm run test:e2e` on the unchanged code and save the pass/fail list as the baseline.
- [ ] **Step 1:** Write `createTarget()`. Move the duplicated `beforeAll` boot block (prefix, ValidationPipe, interceptor, filter, Fastify `ready()`) into it.
- [ ] **Step 2:** In every suite, replace `request(app.getHttpServer())` with `request(target.http)`, `app.get(PrismaService)` with `target.prisma`, and `app.close()` with `target.close()`.
- [ ] **Step 3:** Run `cd apps/api && npm run test:e2e`. Expected: same pass/fail set as the Step 0 baseline.
- [ ] **Step 4:** Start Node (`npm run start:dev`) and run `E2E_BASE_URL=http://localhost:3000 npm run test:e2e`. Expected: same pass set.
- [ ] **Step 5:** Commit `test(api): allow e2e suites to target a running server via E2E_BASE_URL`.

---

### Task 2: Solution scaffold, config, health, CI

**Files:**
- Create: `apps/api-dotnet/global.json`, `PixelPay.sln`, `Directory.Packages.props`, `Directory.Build.props` (nullable, implicit usings, warnings as errors)
- Create projects: `src/PixelPay.Domain`, `src/PixelPay.Application`, `src/PixelPay.Infrastructure`, `src/PixelPay.Api`, `tests/PixelPay.UnitTests`, `tests/PixelPay.IntegrationTests`
- Create: `src/PixelPay.Infrastructure/Config/EnvConfig.cs`, `src/PixelPay.Infrastructure/Config/MySqlUrl.cs`
- Create: `src/PixelPay.Api/Program.cs`, `src/PixelPay.Api/Controllers/HealthController.cs`
- Create: `infrastructure/docker/Dockerfile.api-dotnet.dev`, `infrastructure/docker/Dockerfile.api-dotnet`
- Modify: `.github/workflows/ci.yml` (add job `dotnet`)
- Test: `tests/PixelPay.UnitTests/Config/MySqlUrlTests.cs`, `tests/PixelPay.IntegrationTests/HealthTests.cs`, `tests/PixelPay.IntegrationTests/Support/ApiFactory.cs`

**Interfaces:**
- Produces: `public static string MySqlUrl.ToConnectionString(string url)`
- Produces: `public sealed record EnvConfig(string ConnectionString, string JwtSecret, string JwtRefreshSecret, string JwtExpiresIn, string JwtRefreshExpiresIn, string EncryptionKey, int Port, string FrontendUrl)` with `static EnvConfig Load(IConfiguration c)`. Required values throw `InvalidOperationException("<NAME> is required")` when missing.
- Produces: `ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime`. It starts a `MySqlContainer` (`mysql:8.0`), applies `apps/api/prisma/migrations/20260915040045_init/migration.sql`, and sets the env vars above. Exposes `ApiFactory.ConnectionString`.
- References: Api → Application, Infrastructure; Infrastructure → Application, Domain; Application → Domain.

- [ ] **Step 1: Write the failing test** `MySqlUrlTests`

```csharp
[Fact] public void Parses_full_url() =>
    Assert.Equal("Server=mysql;Port=3306;Database=pixelpay;User ID=pixelpay;Password=p@ss;",
        MySqlUrl.ToConnectionString("mysql://pixelpay:p%40ss@mysql:3306/pixelpay"));
[Fact] public void Defaults_port_3306() =>
    Assert.Contains("Port=3306;", MySqlUrl.ToConnectionString("mysql://u:p@h/db"));
```

- [ ] **Step 2:** `dotnet test tests/PixelPay.UnitTests` → FAIL (type missing).
- [ ] **Step 3:** Implement `MySqlUrl` and `EnvConfig`. In `Program.cs`, listen on `APP_PORT`, use Serilog JSON console, set controllers' JSON options to camelCase, add CORS (origin `FRONTEND_URL`, credentials), and register Swagger in Development only. Make `Program` partial-public for the factory.
- [ ] **Step 4:** Write `HealthTests`: `GET /api/v1/health` returns 200 with a body equal to what `apps/api/src/health/health.controller.ts` returns, wrapped as `{success:true,data:…}`. `/api/v1/health/db` runs `SELECT 1`. `/health/queues` is not part of Phase 1.
- [ ] **Step 5:** `dotnet test` → PASS for both projects.
- [ ] **Step 6:** Write the Dockerfiles. Dev: `mcr.microsoft.com/dotnet/sdk:10.0`, `dotnet watch --project src/PixelPay.Api run`. Prod: multi-stage build onto `mcr.microsoft.com/dotnet/aspnet:10.0`. Add CI job `dotnet` (ubuntu-latest, `actions/setup-dotnet@v4` with `10.0.x`, `dotnet build -c Release`, `dotnet test -c Release`; Testcontainers uses the runner's Docker).
- [ ] **Step 7:** Commit `feat(api-dotnet): scaffold .NET 10 solution with config, health and CI`.

---

### Task 3: Response envelope, error contract, JSON parity

**Files:**
- Create: `src/PixelPay.Api/Envelope/EnvelopeResultFilter.cs`, `src/PixelPay.Api/Envelope/ExceptionEnvelopeMiddleware.cs`, `src/PixelPay.Api/Envelope/NestErrors.cs`
- Create: `src/PixelPay.Application/Common/ApiException.cs`
- Create: `src/PixelPay.Domain/Common/RawJson.cs` (`public readonly record struct RawJson(string Value)`)
- Create: `src/PixelPay.Api/Json/ApiJson.cs`, `src/PixelPay.Api/Json/PrismaDecimalConverter.cs`, `src/PixelPay.Api/Json/UtcDateTimeConverter.cs`, `src/PixelPay.Api/Json/RawJsonConverter.cs`
- Create: `src/PixelPay.Api/Validation/ValidationFilter.cs`, `src/PixelPay.Api/Validation/StrictBodyFilter.cs`
- Test: `tests/PixelPay.UnitTests/Json/ConverterTests.cs`, `tests/PixelPay.IntegrationTests/EnvelopeTests.cs`

**Interfaces:**
- Produces: `public class ApiException(int status, string? message = null) : Exception`. Subclasses `BadRequestException`, `UnauthorizedException`, `ForbiddenException`, `NotFoundException`, `ConflictException`. `UnauthorizedException()` with no message renders `{statusCode:401,message:"Unauthorized"}` (no `error` key). With a message it renders `{statusCode,message,error:"<reason phrase>"}`.
- Produces: `NestErrors.Validation(IEnumerable<string> messages)` → 400 body `{statusCode:400,message:[…],error:"Bad Request"}`.
- Produces: validators are `AbstractValidator<T>` registered in DI. `ValidationFilter` runs the validator for any action argument that has one.
- Produces: `StrictBodyFilter` rejects JSON body properties not on the DTO with `property <name> should not exist` (added to the same validation message list).
- Produces: `[JsonConverter]`-free global converters: `decimal` → normalized string; `DateTime` → `yyyy-MM-ddTHH:mm:ss.fffZ` (Kind Unspecified is treated as UTC); `RawJson` (wrapper over a `string` JSON column) → embedded raw JSON. Null properties are written (`DefaultIgnoreCondition = Never`). Enums are written as string names.
- Also: unknown routes → 404 `{statusCode:404,message:"Cannot GET /api/v1/x",error:"Not Found"}`. Model-binding failures use `NestErrors.Validation`. Unhandled exceptions → 500 `{message:"Internal server error"}`, logged.

- [ ] **Step 1: Write the failing test** `ConverterTests`

```csharp
[Theory]
[InlineData("100.00", "\"100\"")] [InlineData("12.50", "\"12.5\"")]
[InlineData("0.0000", "\"0\"")]   [InlineData("10.1234", "\"10.1234\"")]
public void Decimal_matches_prisma(string v, string json) =>
    Assert.Equal(json, JsonSerializer.Serialize(decimal.Parse(v, CultureInfo.InvariantCulture), ApiJson.Options));
[Fact] public void Date_has_three_fraction_digits_and_Z() =>
    Assert.Equal("\"2026-10-26T04:00:45.100Z\"",
        JsonSerializer.Serialize(new DateTime(2026,10,26,4,0,45,100, DateTimeKind.Unspecified), ApiJson.Options));
```

- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement the converters and the `public static class ApiJson { public static JsonSerializerOptions Options }` used by MVC.
- [ ] **Step 4: Write the failing test** `EnvelopeTests`, using a test-only controller registered from the test project via `ApiFactory.WithWebHostBuilder` and `ApplicationParts`. Cases: returning an object → `{success:true,data:{…}}`; returning `null` → `data:null`; `throw new NotFoundException("Wallet not found")` → 404 `{success:false,error:{statusCode:404,message:"Wallet not found",error:"Not Found"}}`; `throw new UnauthorizedException()` → 401 `error` with exactly the keys `statusCode`, `message`; `throw new InvalidOperationException()` → 500 `error:{message:"Internal server error"}`; `GET /api/v1/nope` → 404 `Cannot GET /api/v1/nope`; POST `{ "name":"a", "foo":1 }` to a DTO without `foo` → 400 with `property foo should not exist` in `error.message`.
- [ ] **Step 5:** Implement the filters and middleware. Run `dotnet test` → PASS.
- [ ] **Step 6:** Commit `feat(api-dotnet): Nest-compatible response envelope, errors and JSON format`.

---

### Task 4: EF Core scaffold of the Prisma schema

**Files:**
- Create: `src/PixelPay.Domain/Entities/*.cs` (scaffolded), `src/PixelPay.Domain/Enums/*.cs`
- Create: `src/PixelPay.Infrastructure/Persistence/PixelPayDbContext.cs` (scaffolded, then edited), `src/PixelPay.Infrastructure/Persistence/DependencyInjection.cs`
- Create: `src/PixelPay.Application/Common/IAppDbContext.cs`
- Create: `apps/api-dotnet/scripts/scaffold.ps1`
- Test: `tests/PixelPay.IntegrationTests/Persistence/DbContextTests.cs`

**Interfaces:**
- Produces: `IAppDbContext` with `DbSet<User> Users`, `DbSet<Wallet> Wallets`, `DbSet<WalletTransaction> WalletTransactions`, `DbSet<Order> Orders`, `DbSet<GameProduct> GameProducts`, `DbSet<Game> Games`, `DbSet<Affiliate> Affiliates`, plus `Task<int> SaveChangesAsync(CancellationToken)` and `DatabaseFacade Database`.
- Produces: C# enums `UserRole`, `OrderStatus`, `PaymentMethod`, `WalletTransactionType`, `ProductType`, `AffiliateStatus`, with members named exactly as in the Prisma enums and mapped with `HasConversion<string>()`.
- Produces: JSON columns (`metadata`, `old_values`, `new_values`) as `RawJson?` properties.
- Produces: `services.AddPersistence(EnvConfig)` using `UseMySql(conn, ServerVersion.Parse("8.0-mysql"))`. `RawJson` maps to the column via a `ValueConverter<RawJson, string>`.
- Produces: `ApiFactory.Db() : PixelPayDbContext`, a fresh context on the test container, used by every later integration test.

- [ ] **Step 1:** Write `scaffold.ps1`: `dotnet ef dbcontext scaffold $conn Pomelo.EntityFrameworkCore.MySql --project src/PixelPay.Infrastructure --context PixelPayDbContext --context-dir Persistence --output-dir ../PixelPay.Domain/Entities --namespace PixelPay.Domain.Entities --context-namespace PixelPay.Infrastructure.Persistence --no-onconfiguring --force`. Exclude `_prisma_migrations` with `--table` for each model table.
- [ ] **Step 2:** Run it against a local MySQL that has the Prisma baseline applied. Move the configuration into `OnModelCreating` partials so re-scaffolding can overwrite entities without touching hand edits. Replace enum `string` properties with C# enums, and JSON columns with `RawJson`.
- [ ] **Step 3: Write the failing test** `DbContextTests`: insert a `User` + `Wallet` via raw SQL matching Prisma's columns, read it through `PixelPayDbContext`, and assert `Role == UserRole.USER`, `Wallet.Balance == 0m`, and `Wallet.UpdatedAt.Kind` handled as UTC by the converter round-trip.
- [ ] **Step 4:** `dotnet test` → PASS.
- [ ] **Step 5:** Commit `feat(api-dotnet): scaffold EF Core 9 model from Prisma MySQL schema`.

---

### Task 5: Crypto parity (AES-256-GCM, HMAC)

**Files:**
- Create: `src/PixelPay.Infrastructure/Security/CryptoUtil.cs`
- Create: `tests/PixelPay.UnitTests/Security/fixtures/node-vectors.json` (generated)
- Create: `apps/api-dotnet/scripts/gen-crypto-vectors.mjs`
- Test: `tests/PixelPay.UnitTests/Security/CryptoUtilTests.cs`

**Interfaces:**
- Produces: `static string CryptoUtil.HmacSha256Hex(string secret, string data)`, `static string CryptoUtil.EncryptAes256(string keyHex, string plaintext)` → `ivHex:tagHex:dataHex` (12-byte IV, 16-byte tag), `static string CryptoUtil.DecryptAes256(string keyHex, string ciphertext)`.

- [ ] **Step 1:** Write `gen-crypto-vectors.mjs`. It imports Node `crypto`, replicates `apps/api/src/common/utils/crypto.ts`, and writes `{ keyHex, plaintext, ciphertext, hmacSecret, hmacData, hmacHex }` with a 64-hex-char key. Run `node scripts/gen-crypto-vectors.mjs > tests/PixelPay.UnitTests/Security/fixtures/node-vectors.json`.
- [ ] **Step 2: Write the failing tests:** `Decrypts_node_ciphertext` (equals `plaintext`), `Hmac_matches_node` (equals `hmacHex`), `Roundtrip` (decrypt(encrypt(x)) == x), `Tampered_tag_throws` (`AuthenticationTagMismatchException`).
- [ ] **Step 3:** Run → FAIL. Implement with `System.Security.Cryptography.AesGcm` (tag size 16) and `HMACSHA256`. Lowercase hex.
- [ ] **Step 4:** Run → PASS. Commit `feat(api-dotnet): AES-GCM and HMAC compatible with Node crypto util`.

---

### Task 6: JWT issuing and validation, guards

**Files:**
- Create: `src/PixelPay.Application/Auth/ITokenService.cs`, `src/PixelPay.Infrastructure/Auth/JwtTokenService.cs`, `src/PixelPay.Infrastructure/Auth/DurationParser.cs`, `src/PixelPay.Api/Auth/AuthSetup.cs`, `src/PixelPay.Api/Auth/CurrentUser.cs`
- Create: `apps/api-dotnet/scripts/gen-jwt-fixture.mjs`, `tests/PixelPay.UnitTests/Auth/fixtures/node-token.json`
- Test: `tests/PixelPay.UnitTests/Auth/DurationParserTests.cs`, `tests/PixelPay.UnitTests/Auth/JwtTokenServiceTests.cs`, `tests/PixelPay.IntegrationTests/Auth/GuardTests.cs`

**Interfaces:**
- Produces: `record TokenPair(string AccessToken, string RefreshToken)`; `ITokenService.Issue(string userId, string email, string role) : TokenPair`; `ITokenService.ValidateRefresh(string token) : (string Sub, string Email, string Role)?` (null when invalid).
- Produces: `static TimeSpan DurationParser.Parse(string v)`. Accepts `Ns`, `Nm`, `Nh`, `Nd` and a bare integer (seconds), like `ms`/jsonwebtoken. Anything else throws `FormatException`.
- Produces: `record CurrentUser(string Id, string Email, string Role)`, bound in controllers via `[FromCurrentUser]` or `HttpContext.GetCurrentUser()`.
- Produces: `[Authorize]` failure → 401 `{statusCode:401,message:"Unauthorized"}`. `[Authorize(Roles="ADMIN")]` failure → 403 `{statusCode:403,message:"Forbidden resource",error:"Forbidden"}` (via `IAuthorizationMiddlewareResultHandler`). Bearer settings: `MapInboundClaims=false`, `RoleClaimType="role"`, `NameClaimType="sub"`, `ClockSkew=TimeSpan.Zero`, no issuer or audience validation, key = UTF-8 bytes of `JWT_SECRET`.

- [ ] **Step 1:** Write `gen-jwt-fixture.mjs` using `jsonwebtoken` from `apps/api/node_modules`. It signs `{sub:"00000000-0000-0000-0000-000000000001",email:"a@b.c",role:"ADMIN"}` with the CI test secret and `expiresIn: "3650d"`. Write `{ secret, token }` to the fixture.
- [ ] **Step 2: Write the failing tests:** `DurationParser` (`"15m"`→15 min, `"7d"`→7 days, `"3600"`→1 h, `"abc"` throws); `Node_token_validates_with_role_claim_named_role`; `Issued_access_token_has_only_sub_email_role_iat_exp_claims` (plus `nbf` allowed); `Refresh_token_signed_with_access_secret_is_rejected`.
- [ ] **Step 3:** Run → FAIL. Implement with `Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler`.
- [ ] **Step 4: Write the failing test** `GuardTests` with test controllers: no token → 401 body shape; token expired 1 s ago → 401; USER token on an ADMIN action → 403 body shape; Node fixture token → 200.
- [ ] **Step 5:** Run → PASS. Commit `feat(api-dotnet): JWT auth compatible with Node tokens`.

---

### Task 7: Auth endpoints

**Files:**
- Create: `src/PixelPay.Application/Auth/AuthService.cs`, `src/PixelPay.Application/Auth/Dtos.cs`, `src/PixelPay.Application/Auth/Validators.cs`, `src/PixelPay.Application/Common/IPasswordHasher.cs`, `src/PixelPay.Infrastructure/Auth/BcryptPasswordHasher.cs`, `src/PixelPay.Infrastructure/Auth/ReferralCode.cs`, `src/PixelPay.Api/Controllers/AuthController.cs`
- Test: `tests/PixelPay.IntegrationTests/Auth/AuthEndpointsTests.cs`

**Interfaces:**
- Consumes: `ITokenService`, `IAppDbContext`, `ApiException` types, `CurrentUser`.
- Produces routes (status codes):
  - `POST auth/register` 201 → `{ user:{id,email,displayName,role,referralCode}, accessToken, refreshToken }`. Duplicate email → 409 `Email already registered`. If `referredBy` matches a user's `referralCode`, set `referred_by` and upsert that user's `affiliates` row (create with `commissionRate 2.00`, `ACTIVE`, `totalReferrals 1`, `referralCode` = referrer's code, otherwise increment `totalReferrals`). Creates the wallet (`0`/`0`, `THB`). The user's referral code is 8 chars from `A–Z0–9`.
  - `POST auth/login` 200 → `{ user:{id,email,role}, accessToken, refreshToken }`. Missing `email` or `password` → 401 `{statusCode:401,message:"Unauthorized"}` (passport-local behaviour, **not** 400). Unknown email, inactive user or wrong password → 401 `{statusCode:401,message:"Invalid credentials",error:"Unauthorized"}`. Unknown body properties are allowed here (no DTO validation in Node).
  - `POST auth/refresh` 200 → `{ accessToken, refreshToken }`. Invalid → 401 `Invalid or expired refresh token`. Missing `refreshToken` → 400 validation. No auth guard.
  - `GET auth/me` (auth) → `{ id, email, role }` from the token.
  - `POST auth/logout` (auth) 201 → `{ message:"Logged out successfully" }`.
- Validation (class-validator text, order-insensitive): register `email` → `email must be an email`; `password` → `password must be longer than or equal to 8 characters`, `password must be shorter than or equal to 72 characters`, `password must contain uppercase, lowercase and number` (regex `^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)`), `password must be a string`; `displayName` → `displayName must be longer than or equal to 2 characters`, `displayName must be shorter than or equal to 50 characters`, `displayName must be a string`; `referredBy` optional string. refresh: `refreshToken must be a string`.
- `IPasswordHasher.Hash(string) : string` (BCrypt cost 12, `$2b$`); `Verify(string pw, string hash) : bool`.

- [ ] **Step 1: Write the failing tests** (one per bullet above). Include: `Login_accepts_hash_created_by_node_bcrypt` (insert a user whose `password_hash` is a fixed `$2b$12$…` hash generated once with Node `bcrypt.hash('Test1234!',12)` and pasted into the test); `Register_with_unknown_property_returns_400_property_should_not_exist`; `Register_with_referral_upserts_affiliate`.
- [ ] **Step 2:** Run → FAIL. Implement. Register runs in one EF transaction (user + wallet), then the affiliate upsert, the same order as Node.
- [ ] **Step 3:** Run → PASS. Run Node `auth.e2e-spec.ts` against .NET: `E2E_BASE_URL=http://localhost:<dotnet port> npx jest --config test/jest-e2e.json auth` → PASS.
- [ ] **Step 4:** Commit `feat(api-dotnet): auth endpoints`.

---

### Task 8: Users endpoints

**Files:**
- Create: `src/PixelPay.Application/Users/UsersService.cs`, `src/PixelPay.Application/Users/Dtos.cs`, `src/PixelPay.Application/Users/Validators.cs`, `src/PixelPay.Api/Controllers/UsersController.cs`
- Test: `tests/PixelPay.IntegrationTests/Users/UsersEndpointsTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext`, `IPasswordHasher`, `CurrentUser`.
- Produces routes (all auth):
  - `GET users/profile` → `{ id, email, phone, displayName, avatarUrl, role, isVerified, referralCode, createdAt }` (nulls present). A missing user → 500 (Prisma `findUniqueOrThrow` parity).
  - `PATCH users/profile` 200 → `{ id, email, displayName, avatarUrl }`. Updates only the fields present and sets `updated_at`. `displayName` 2–50 when present.
  - `PUT users/change-password` 200 → `{ message:"Password changed successfully" }`. Wrong current password → 400 `Current password is incorrect`. `newPassword`: same rules as register except the regex message `newPassword must match /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/ regular expression`.

- [ ] **Step 1:** Write failing tests for each route plus `Profile_includes_null_phone`.
- [ ] **Step 2:** Run → FAIL, implement, run → PASS. Run `users.e2e-spec.ts` against .NET → PASS.
- [ ] **Step 3:** Commit `feat(api-dotnet): users endpoints`.

---

### Task 9: Wallet endpoints

**Files:**
- Create: `src/PixelPay.Application/Wallet/WalletService.cs`, `src/PixelPay.Application/Wallet/Dtos.cs`, `src/PixelPay.Application/Wallet/Validators.cs`, `src/PixelPay.Api/Controllers/WalletController.cs`
- Test: `tests/PixelPay.IntegrationTests/Wallet/WalletEndpointsTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext`, `CurrentUser`, `[Authorize(Roles="ADMIN")]`.
- Produces: `WalletService.CreditAsync(string walletId, decimal amount, WalletTransactionType type, string description, string? referenceId = null, string? referenceType = null) : Task<(Wallet Wallet, WalletTransaction Transaction)>`. It runs inside the caller's transaction, reads `balance` as `balanceBefore`, and runs `ExecuteUpdateAsync(balance = balance + amount, updated_at = now)`. It inserts a transaction row with `balanceAfter = balanceBefore + amount` and `metadata = {"description": description}`. Phase 2 adds `Debit`, `Lock`, `Unlock`, `DebitLocked` beside it.
- Produces routes (all auth):
  - `GET wallet` → wallet row `{id,userId,balance,lockedBalance,currency,updatedAt,transactions:[…latest 20, created_at desc]}`. No wallet → 404 `Wallet not found`.
  - `GET wallet/transactions?page=1&limit=20` → `{ items, total, page, limit }`.
  - `POST wallet/deposit` (ADMIN) 201 → `{ wallet, transaction }`. Body `userId` (non-empty string; messages `userId should not be empty`, `userId must be a string`), `amount` (`amount must be a positive number`, `amount must be a number conforming to the specified constraints`), optional `description` (default `Admin manual deposit`). No wallet → 404 `Wallet not found for user`.

- [ ] **Step 1: Write the failing tests** for each route, plus:

```csharp
[Fact] public async Task Balance_is_string_without_trailing_zeros()
{ /* deposit 100 → GET wallet */ Assert.Equal("100", json.GetProperty("data").GetProperty("balance").GetString()); }

[Fact] public async Task Parallel_deposits_do_not_lose_updates()
{ /* 20 concurrent POST wallet/deposit amount 5 */ Assert.Equal(100m, dbBalance); Assert.Equal(20, txRowCount); }
```

- [ ] **Step 2:** Run → FAIL, implement, run → PASS. Run `wallet.e2e-spec.ts` against .NET → PASS.
- [ ] **Step 3:** Commit `feat(api-dotnet): wallet endpoints`.

---

### Task 10: Order read endpoints

**Files:**
- Create: `src/PixelPay.Application/Orders/OrdersQueryService.cs`, `src/PixelPay.Api/Controllers/OrdersController.cs`
- Test: `tests/PixelPay.IntegrationTests/Orders/OrdersReadTests.cs`

**Interfaces:**
- Consumes: `IAppDbContext`, `CurrentUser`.
- Produces routes (auth, GET only — POST create/cancel stay on Node until Phase 2):
  - `GET orders?page&limit` → `{ items:[order rows], total, page, limit }`, `created_at` desc, current user only.
  - `GET orders/{id}` → order row + `gameProduct` (full row) + `gameProduct.game` (full row). Missing → 404 `Order not found`. Another user's order → 401 `{statusCode:401,message:"Unauthorized"}` (Node parity).

- [ ] **Step 1:** Write failing tests for both routes, the 404, the foreign-order 401, and `Order_json_has_string_money_and_raw_metadata` (`totalPrice` `"49.5"`, `metadata` an object, not a string).
- [ ] **Step 2:** Run → FAIL, implement, run → PASS. Run the GET cases of `orders.e2e-spec.ts` against .NET (`-t "GET"`) → PASS.
- [ ] **Step 3:** Commit `feat(api-dotnet): order read endpoints`.

---

### Task 11: YARP gateway and side-by-side compose

**Files:**
- Create: `src/PixelPay.Gateway/Program.cs`, `src/PixelPay.Gateway/appsettings.json`, `infrastructure/docker/Dockerfile.gateway.dev`, `infrastructure/docker/Dockerfile.gateway`
- Modify: `docker-compose.yml`
- Test: `tests/PixelPay.IntegrationTests/Gateway/RouteTableTests.cs`

**Interfaces:**
- Produces routes (`ReverseProxy:Routes`). Cluster `dotnet` = `NET_API_URL`, cluster `node` = `NODE_API_URL` (env):
  - `dotnet`: `/api/v1/auth/{**rest}`, `/api/v1/users/{**rest}`, `/api/v1/wallet/{**rest}`, `/api/v1/wallet`, `GET /api/v1/orders`, `GET /api/v1/orders/{id}`
  - `node` (lowest priority, `Order: 1000`): `/{**rest}`. This includes `/socket.io`, which YARP proxies as WebSockets by default.
- Compose: `api` (Node) host port `4000` → `4010`; new `api-dotnet` (host `4002` → `3000`, bind-mount `apps/api-dotnet`, same env as `api`); new `gateway` (host `4000` → `8080`, `NODE_API_URL=http://api:3010`, `NET_API_URL=http://api-dotnet:3000`); `web` `API_URL_INTERNAL=http://gateway:8080/api/v1`.

- [ ] **Step 1: Write the failing test** `RouteTableTests`. Load `appsettings.json` and assert, using YARP's route matching via `WebApplicationFactory` with stub clusters, that `POST /api/v1/orders` → node, `GET /api/v1/orders/abc` → dotnet, `POST /api/v1/orders/abc/cancel` → node, `GET /api/v1/games` → node, `POST /api/v1/payments/webhook` → node, `GET /api/v1/wallet` → dotnet.
- [ ] **Step 2:** Run → FAIL, implement, run → PASS.
- [ ] **Step 3:** `docker compose up --build`. Then `E2E_BASE_URL=http://localhost:4000 npm run test:e2e` in `apps/api` → same pass set as the Task 1 baseline.
- [ ] **Step 4:** Add the cross-runtime case to `apps/api/test/auth.e2e-spec.ts`. When `E2E_NODE_URL` is also set, log in on `E2E_NODE_URL` and call `GET /api/v1/wallet` on `E2E_BASE_URL` with that token → 200. Run it with `E2E_NODE_URL=http://localhost:4010` → PASS.
- [ ] **Step 5:** Open `http://localhost:4001` in the browser: register, log in, view the wallet and order list, and confirm no console/network errors.
- [ ] **Step 6:** Extend the CI `dotnet` job. Apply the Prisma migration, start Node (`npm run build` then `node dist/main.js`; check the emitted path), `dotnet run` Api and Gateway in the background, wait on `/api/v1/health`, then run `E2E_BASE_URL=http://localhost:8080 E2E_NODE_URL=http://localhost:3000 npm run test:e2e`.
- [ ] **Step 7:** Commit `feat(api-dotnet): YARP gateway routing Phase 1 paths to .NET`.

---

## Phase 1 done when

- All 9 e2e suites pass through the gateway with the Phase 1 routes on .NET.
- `dotnet test` is green in CI.
- The Phase 1 rollback works: deleting the `dotnet` routes from `appsettings.json` and restarting the gateway sends everything to Node (one manual check).
