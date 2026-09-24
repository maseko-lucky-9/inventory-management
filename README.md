# Inventory Management API

> **A RESTful inventory API in .NET (C#) over PostgreSQL, with a small Vue UI: products, warehouses, stock levels and warehouse-to-warehouse transfers that never oversell under concurrency. Tier built: Senior.**

> **Status: pre-build.** This README was drafted before the four-hour clock started, as design preparation, together with `CLAUDE.md`, `docs/decisions/`, `docs/spec.md` and `docs/constitution.md`. No application code exists yet. Nothing below claims that anything builds, runs or passes. Sections marked **Completed at T21** state the intended contract now and get real results during the build. The time log and the AI commit list are completed at the T22 pre-submit gate. These pre-clock documents were drafted with an AI assistant (Claude Code) from my own design notes. That is disclosed in [section 13](#13-ai-usage).

## Constraints this repo follows

Each rule comes from the assignment. The trace IDs are from my spec: H = hard constraint, T = tier obligation, D = drop order, G = gap resolution I derived. Each rule has a gate, so tooling enforces it, not memory.

| Constraint | Trace | Enforced by |
|---|---|---|
| PostgreSQL only: `postgres:17-alpine` in Compose, in Testcontainers and in the `docker run` one-liner. No SQLite, SQL Server or in-memory provider anywhere, tests included | H2, T3 | `scripts/check-constraints.sh` package deny-list (pre-commit hook and pre-submit gate); the integration suite runs on `postgres:17-alpine` |
| No ORM and no SQL-generating helper. Dapper over Npgsql. Banned: EntityFrameworkCore, NHibernate, ServiceStack, OrmLite, Dapper.Contrib, Dapper.SimpleCRUD, Dapper.FastCrud, SqlKata, linq2db, RepoDb, and anything with Sqlite, SqlClient or InMemory | H3, G4 | The same deny-list scan |
| Hand-written SQL with named parameters only. SQL lives only in `*Store` files | H3 | `SqlHygieneTests` (unit suite, every commit): Store files may not interpolate, format or concatenate SQL; no other file may contain SQL keywords |
| Schema shipped in `db/schema.sql` and `db/seed.sql`, idempotent, applied by the API at startup | H4, G3 | Integration suite on a fresh container; a test applies the schema twice; fresh-clone smoke test |
| Conventional Commits with a mandatory scope from a fixed list; subject at most 72 characters (regex below) | H6, G25 | `.githooks/commit-msg`; pre-submit history audit |
| One logical change per commit: one task, one commit, one green suite. Never "wip", "final", a bare "fix", or one commit containing everything | H6 | Pre-commit hook blocks a red build or red unit tests; history audit |
| Never squash. Rebase or fast-forward merges only; no merge commits | H6 | `git log --merges` must be empty at pre-submit |
| Four hours maximum. Cut line at 3:20: if authentication is not green, drop the e2e spec, then Compose, in that order, and record the cuts here | H5, D1–D10 | Time log in [section 14](#14-time-log); task timeboxes |
| AI use disclosed; every line explainable | H8, G26 | Commit footer `AI-assisted: <tool> — reviewed and explained`; [section 13](#13-ai-usage); an explain-back checkpoint per task |
| No secrets in git | G22 | Secrets live only in a local `.env` (gitignored), created from the committed `.env.example`, which holds placeholders only. The JWT signing key and the demo users' password come from it |

Commit subject regex:

```text
^(feat|fix|test|docs|chore|refactor|build|ci)\((repo|api|db|products|warehouses|stock|orders|auth|ui|e2e|compose|readme|tests)\): [a-z].{8,}$
```

Activate the hooks once per clone. The pre-submit history audit is the last three commands; each must print nothing.

```bash
git config core.hooksPath .githooks

git log --format=%s | grep -Ev '^(feat|fix|test|docs|chore|refactor|build|ci)\((repo|api|db|products|warehouses|stock|orders|auth|ui|e2e|compose|readme|tests)\): [a-z].{8,}$'
git log --format=%s | awk 'length > 72'
git log --merges --oneline
```

If SCAD sends a starter repository, its README takes precedence over this design (A1). Its code pattern is followed even where I disagree, and the objection is recorded here (A4).

## 1. Problem statement and tier

Build a RESTful inventory API in .NET with a small UI: products, warehouses, stock and warehouse-to-warehouse transfers, with data integrity, edge-case handling and a concurrency strategy I can defend at a 45-minute interview. Four hours.

- Tier targeted: **Senior**. That means all endpoints, an implemented concurrency strategy, integration tests on a real PostgreSQL, the UI create form with its states, authentication, warehouse-scoped authorization in the data layer, Docker Compose and one Playwright spec.
- The recruiter has not yet confirmed the tier. Trimming to a lower tier is deletion along the drop order, never a redesign.
- Build order follows SCAD's drop order D1–D10: the transfer and its tests first, then validation and errors, concurrency, the integration test, the README, the UI list and detail, the UI create form, authentication, Compose, and the e2e spec.
- Tier actually delivered, and anything cut: **Completed at T21.**

## 2. Prerequisites

| Tool | Version | Used for |
|---|---|---|
| .NET SDK | 10 (LTS) `[verify with dotnet --list-sdks on the day]`. Pinned by `global.json`, rolling forward to the latest feature band | Build, run, unit and integration tests |
| Docker | Engine running; image `postgres:17-alpine` | Database, Compose, Testcontainers |
| Node.js | LTS | The Vue UI (Vite) and the Playwright e2e spec |
| Git | Any recent | Hooks in `.githooks/` |
| curl, jq | Any recent | The example calls in section 5 (jq only extracts the token) |

On a non-default Docker runtime (Colima, Rancher Desktop), set the Docker host / socket override environment variables that Testcontainers needs before running the integration tests.

Exact versions used for the build: **Completed at T21.**

## 3. Quick start

### Option A: Docker Compose (API + PostgreSQL)

```bash
git clone <private-repo-url> inventory-management
cd inventory-management
cp .env.example .env      # then replace each <PLACEHOLDER> (see the comments in the file)
docker compose up
```

Compose starts `postgres:17-alpine` with a `pg_isready` health check and a named volume. The API starts only once the database reports healthy. It gets its connection string and secrets from the environment (`.env`), applies `db/schema.sql` and `db/seed.sql` at startup, and hashes `DemoUsers__Password` for the demo users. Compose runs the API and the database only, as the assignment describes (question 6 in section 12).

### Option B: the assignment's `docker run` one-liner + `dotnet run`

1. Start PostgreSQL with the assignment's one-liner, copied verbatim from its §3.1. The `postgres` password is the assignment's own public value, not a secret (G22).

```bash
docker run --name scad-test-db \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 -d postgres:17-alpine
```

2. Create `.env` from the example (skip if you already did for option A), load it, and run the API. Its default connection string uses exactly the one-liner's values. On startup it takes a PostgreSQL advisory lock, applies the schema and seed, and hashes the demo users' password from `.env`.

```bash
cp .env.example .env      # then replace each <PLACEHOLDER>
set -a; . ./.env; set +a
dotnet run --project src/Inventory.Api
```

Manual fallback, only if you want to create the schema yourself (both files are idempotent, so running them again is safe):

```bash
psql -h localhost -p 5432 -U postgres -d postgres -f db/schema.sql
psql -h localhost -p 5432 -U postgres -d postgres -f db/seed.sql
```

### The UI (development)

```bash
cd ui
npm install
npm run dev
```

The Vite dev server proxies API calls to the API, so the SPA and the API share one origin. The API has no CORS policy.

The API base URL and published ports are recorded here: **Completed at T21.**

## 4. Running the tests

```bash
# Unit tests only: validators, TransferService (fake store), DbErrorTranslator, source scans.
# No Docker needed. The pre-commit hook runs these.
dotnet test tests/Inventory.UnitTests

# Unit + integration. Integration tests go through the real HTTP pipeline (WebApplicationFactory)
# against a fresh postgres:17-alpine container (Testcontainers). Docker must be running.
dotnet test

# Package deny-list scan (must print nothing and exit 0).
scripts/check-constraints.sh
```

End-to-end: one Playwright spec, run against the composed stack and the Vite dev server.

```bash
docker compose up -d
# in a second terminal: cd ui && npm install && npm run dev
cd e2e
npm install
npx playwright install chromium
npx playwright test
```

The integration suite starts one PostgreSQL container per run and uses unique data per test, so there is no cleanup code. Results: see [section 10](#10-test-catalogue). **Completed at T21.**

## 5. Example calls

`api.http` holds the same sequence as ready-to-send requests. The built-in OpenAPI document is served in Development; its path is **Completed at T21**.

### API surface

| Method and path | Auth | Scope | Request fields | Success | Errors |
|---|---|---|---|---|---|
| `POST /auth/login` | None (rate-limited) | — | `username`, `password` | 200 `accessToken`, `tokenType`, `expiresAt` | 400, 401 `invalid_credentials`, 429 |
| `GET /products` | Bearer | Global | — | 200 list of `code`, `description`, `createdAt` | 401 |
| `GET /products/{code}` | Bearer | Global | — | 200 `code`, `description`, `createdAt` | 404 `product_not_found` |
| `POST /products` | Bearer | Global | `code`, `description` | 201 + `Location: /products/{code}` + body | 400, 409 `duplicate_product_code` |
| `GET /warehouses` | Bearer | Linked only | — | 200 list of `code`, `name` | 401 |
| `POST /warehouses` | Bearer | Creator auto-linked | `code`, `name` | 201 + body | 400, 409 `duplicate_warehouse_code` |
| `POST /stock` | Bearer | Target linked | `productCode`, `warehouseCode`, `quantity` | 200 `productCode`, `warehouseCode`, `quantity` (resulting level) | 400 `validation_failed` / `unknown_*` / `quantity_out_of_range` |
| `GET /stock?productCode=&warehouseCode=` | Bearer | Linked only | At least one filter | 200 list of `productCode`, `warehouseCode`, `quantity`, `updatedAt` | 400, 404 `product_not_found` / `warehouse_not_found` |
| `POST /orders` | Bearer | Source linked; destination any existing warehouse | `productCode`, `sourceWarehouseCode`, `destinationWarehouseCode`, `quantity` | 201 `id`, the four request fields, `createdAt` | 400 `validation_failed` / `unknown_*` / `insufficient_stock` / `quantity_out_of_range`; 503 `concurrency_conflict` |
| `GET /orders` (D10, optional) | Bearer | Source or destination linked | — | 200 list, newest first | 401 |
| `GET /health` | None | — | — | 200 healthy / 503 unhealthy | — |

Bodies are JSON with camelCase names exactly as the assignment spells them. Success bodies are the resource or a plain array, with no wrapper. Timestamps are ISO-8601 UTC. Unknown request fields are ignored. `POST /stock` is additive and returns 200 with the resulting level, because an increment creates no new addressable resource. Lists are not paginated (the assignment says "list all").

### Error contract

Every non-2xx response is an RFC 9457 Problem Details document with `type`, `title`, `status`, `detail`, `instance`, a stable `code` and a `traceId`. Validation failures add `errors`, keyed by JSON property name. The `traceId` matches the server log line.

| Condition | Status | `code` |
|---|---|---|
| Malformed JSON, wrong types, empty body | 400 | `malformed_request` |
| Field rule broken, self-transfer, missing stock filter | 400 | `validation_failed` |
| Unknown or unlinked code in a request body | 400 | `unknown_product_code` / `unknown_warehouse_code` |
| Insufficient stock (detail names product, source, requested, available) | 400 | `insufficient_stock` |
| Arithmetic overflow on a stock level | 400 | `quantity_out_of_range` |
| Missing or invalid token | 401 | `unauthorized` |
| Wrong username or password | 401 | `invalid_credentials` |
| Unknown or unlinked code in a path or query | 404 | `product_not_found` / `warehouse_not_found` |
| Duplicate code | 409 | `duplicate_product_code` / `duplicate_warehouse_code` |
| Too many login attempts | 429 | `too_many_requests` |
| Deadlock, serialization failure or lock timeout | 503 + Retry-After | `concurrency_conflict` |
| Database unreachable | 503 | `database_unavailable` |
| Check-constraint violation (should be impossible; logged as a defect) | 500 | `internal_error` |
| Anything else (no stack trace) | 500 | `internal_error` |

An unknown code in a request body is a 400, not a 404. RFC 9110 defines 404 as "target resource not found". For `POST /orders` the target, `/orders`, exists; the problem is a reference inside the body.

### Curl sequence

The comments show the status the design expects. Recorded outputs: **Completed at T21.**

```bash
API="<api-base-url>"   # the URL printed by dotnet run, or the port published by Compose
DEMO_PASS=$(sed -n 's/^DemoUsers__Password=//p' .env)   # the demo users' password you set in .env

# Log in as alice (linked to WH-A)
TOKEN=$(curl -s -X POST "$API/auth/login" -H 'Content-Type: application/json' \
  -d "{\"username\":\"alice\",\"password\":\"$DEMO_PASS\"}" | jq -r .accessToken)
AUTH="Authorization: Bearer $TOKEN"
JSON='Content-Type: application/json'

# Products (global catalogue)
curl -i -X POST "$API/products" -H "$AUTH" -H "$JSON" -d '{"code":"SKU-1","description":"Demo product"}'  # 201 + Location
curl -i -X POST "$API/products" -H "$AUTH" -H "$JSON" -d '{"code":"SKU-1","description":"Demo product"}'  # 409 duplicate_product_code
curl -i "$API/products" -H "$AUTH"                                                                          # 200
curl -i "$API/products/SKU-1" -H "$AUTH"                                                                    # 200
curl -i "$API/products/NO-SUCH-SKU" -H "$AUTH"                                                              # 404 product_not_found

# Warehouses (list is scoped; the creator is linked automatically)
curl -i -X POST "$API/warehouses" -H "$AUTH" -H "$JSON" -d '{"code":"WH-1","name":"Demo warehouse"}'      # 201
curl -i "$API/warehouses" -H "$AUTH"                                                                        # 200: WH-A and WH-1 only

# Stock (additive receipt into a linked warehouse)
curl -i -X POST "$API/stock" -H "$AUTH" -H "$JSON" -d '{"productCode":"SKU-1","warehouseCode":"WH-A","quantity":10}'  # 200, quantity 10
curl -i "$API/stock?productCode=SKU-1" -H "$AUTH"                                                           # 200: linked warehouses only
curl -i "$API/stock?warehouseCode=WH-A" -H "$AUTH"                                                          # 200

# Transfer: WH-A to WH-B (the destination may be any existing warehouse)
curl -i -X POST "$API/orders" -H "$AUTH" -H "$JSON" \
  -d '{"productCode":"SKU-1","sourceWarehouseCode":"WH-A","destinationWarehouseCode":"WH-B","quantity":7}'  # 201
curl -i -X POST "$API/orders" -H "$AUTH" -H "$JSON" \
  -d '{"productCode":"SKU-1","sourceWarehouseCode":"WH-A","destinationWarehouseCode":"WH-B","quantity":7}'  # 400 insufficient_stock: requested 7, available 3

# Scoping: WH-B is not linked to alice, so it looks unknown
curl -i "$API/stock?warehouseCode=WH-B" -H "$AUTH"   # 404 warehouse_not_found (log in as bob and the same call returns 200)

# No token, and health
curl -i "$API/products"   # 401 unauthorized
curl -i "$API/health"     # 200 healthy
```

## 6. Demo users (demo-only)

Password: the value you set as `DemoUsers__Password` in your local `.env` (see `.env.example`); demo-only, never reuse it elsewhere. No password or hash is committed: `db/seed.sql` creates the users and links, and the API hashes the configured password with the framework hasher at startup. If it isn't set, the demo users cannot log in and the API logs a warning.

| Username | Linked warehouses | What it shows |
|---|---|---|
| `alice` | `WH-A` | Sees only WH-A. Can transfer out of WH-A into any existing warehouse |
| `bob` | `WH-B` | Sees only WH-B. The other half of the scoping demo |
| `carol` | none | Sees an empty warehouse list and no stock. Can still create products and warehouses; a warehouse she creates is linked to her at once |

The seed also creates `WH-C`, linked to nobody. It seeds no products and no stock.

## 7. Concurrency strategy and its cost

> **Concurrency strategy.** Transfers use a conditional `UPDATE` with a guard predicate (`quantity >= requested`) inside a single READ COMMITTED transaction, backed by a `CHECK (quantity >= 0)` constraint, and touch the two stock rows in ascending warehouse-id order. PostgreSQL re-evaluates the guard after any lock wait, so two simultaneous transfers of the same stock are serialised by the row lock and the second is refused if the first used the stock up — no application-level read-check-write exists. **Cost:** transfers of the same product out of the same warehouse queue behind each other for the length of one transaction, so a hot item is limited to roughly one transfer per transaction latency; waiting requests hold pooled connections, so a very hot item could exhaust the pool (mitigated with `lock_timeout`/`statement_timeout`); the "available" figure in the refusal message comes from a follow-up read and can already be stale when the client reads it; and part of the business rule lives in SQL, so it is proven by integration tests rather than unit tests. In exchange: one statement decides, there is no retry loop, and correctness does not depend on how many API instances run.

The race walkthrough, lock ordering, the Dekker's algorithm evaluation and the rejected alternatives are in [ADR-003](docs/decisions/003-guarded-update-for-concurrent-transfers.md). The tests that prove it, and the mutation checks that prove those tests can fail, are in [section 10](#10-test-catalogue).

## 8. Why authorization is enforced in the data layer, not the UI

> **Authorization lives in the data layer.** Every query that reads warehouses or stock, and the transfer's source lookup, joins the caller's warehouse links inside the SQL, so data outside the caller's scope is never read. The UI is the wrong place to enforce this: it runs on the user's machine and is fully under their control — anyone can call the API directly with curl or Postman and skip the UI entirely, so a UI check only hides data, it does not protect it. Enforcing at the endpoint level would be better but is forgettable per route; putting the predicate in the store means a new endpoint that reuses the store inherits the rule. Row-level security in PostgreSQL would be the next layer of defence in depth.

How it is scoped: `ICurrentUser` supplies only the user id. One shared link-join fragment on `user_warehouses` is applied inside every scoped query in `WarehouseStore`, `StockStore` and `TransferStore`. Products are a global catalogue. A transfer's source must be linked; its destination may be any existing warehouse. An unlinked warehouse gets the same response as an unknown one. Details: [ADR-006](docs/decisions/006-self-issued-jwt-and-relational-warehouse-scoping.md).

## 9. Design decisions

Each ADR records the context, the alternatives, the cost accepted, the proving tests and the trigger to revisit it. Its `Traces:` line cites requirements (FR-xxx) from the [specification](docs/spec.md) and principles (I–VIII) from the [constitution](docs/constitution.md).

1. [ADR-001: PostgreSQL with Dapper, no ORM](docs/decisions/001-postgresql-with-dapper-no-orm.md). Hand-written, named-parameter SQL in Store files only; Dapper maps rows and generates no SQL.
2. [ADR-002: Vertical slices in one API project](docs/decisions/002-vertical-slices-in-one-api-project.md). Five feature slices plus `Shared/`; interfaces only for `ITransferStore` and `ICurrentUser`.
3. [ADR-003: Guarded update for concurrent transfers](docs/decisions/003-guarded-update-for-concurrent-transfers.md). Guarded decrement, `CHECK (quantity >= 0)`, ascending warehouse-id lock order, one READ COMMITTED transaction.
4. [ADR-004: Domain exceptions, Problem Details and FluentValidation](docs/decisions/004-domain-exceptions-problem-details-and-fluentvalidation.md). One handler, one envelope, `DbErrorTranslator` for database errors.
5. [ADR-005: Schema applied idempotently at startup](docs/decisions/005-schema-applied-idempotently-at-startup.md). `SchemaInitializer` under an advisory lock; works with the one-liner, Compose and the tests.
6. [ADR-006: Self-issued JWT and relational warehouse scoping](docs/decisions/006-self-issued-jwt-and-relational-warehouse-scoping.md). The API signs its own tokens; scope is a join on `user_warehouses` in every scoped query.
7. [ADR-007: Vue and Vite UI with a dev proxy](docs/decisions/007-vue-vite-ui-with-dev-proxy.md). Vue 3 + Vite + TypeScript, explicit page states, same-origin proxy, no CORS.

## 10. Test catalogue

**Completed at T21.** This is the planned catalogue. Test names state their claim, and each row says what the test proves and what it does not prove. Pass/fail results and any renamed tests go here during the build. Planned counts: 1 end-to-end, about 16 integration, about 12 unit.

| Test (named as its claim) | Level | Proves | Does not prove |
|---|---|---|---|
| Transfer succeeds and returns the order when the store reports success | Unit | The service returns the order unchanged | Any SQL |
| Transfer refusal names product, source, requested and available | Unit | Message composition and exception type | That the numbers are current |
| Self-transfer is rejected by the validator | Unit | The rule and its message | HTTP wiring |
| Unique violation becomes a duplicate-code error | Unit | Translator mapping (duplicate code) | That the constraint exists |
| Store and endpoint sources contain no interpolated SQL | Unit (source scan) | H3 hygiene | Query correctness |
| Every scoped store query references the scope fragment | Unit (source scan) | T13 predicate present in every scoped query | That the join is written correctly |
| Posting a product twice returns 201 then 409 naming the code | Integration | Constraint + translation + handler + status | Concurrency of duplicates |
| Transfer moves stock and writes one order atomically | Integration | End-to-end transfer through HTTP | Contention behaviour |
| A transfer that would overflow the destination returns 400 and leaves the source unchanged | Integration | Atomicity: the decrement happened, then rolled back | Other failure points |
| Ten parallel transfers never oversell | Integration | C1 invariant under real contention | That a specific interleaving occurred |
| A blocked transfer re-checks against committed stock | Integration | The READ COMMITTED re-check mechanism | Throughput |
| Opposing transfers never surface a deadlock | Integration | Lock ordering | Other multi-row paths |
| A user cannot see stock in an unlinked warehouse | Integration | Data-layer scoping | UI rendering |
| Requests without a token are rejected with 401 | Integration | Authentication on every route | Token forgery resistance beyond validation |
| Schema applies twice without error on a fresh database | Integration | H4 idempotent delivery | Migration of existing data |
| Every error response carries code and traceId | Integration | B2 envelope consistency | Message quality |
| Creating a duplicate product shows the error on the code field | End-to-end | U3 write path and error surfacing | Other browsers |

Mutation checks. Each is run once during the build; a test that stays green against broken code is deleted and rewritten. Results: **Completed at T21.**

| Mutation | Expected red test |
|---|---|
| Delete the guard predicate only | Parallel oversell, lock interleaving (the CHECK fires and returns 500, not 400) |
| Delete the guard and the CHECK constraint | Parallel oversell (stock goes negative) |
| Replace the guard with a C# read, compare and unguarded write | Lock interleaving (a 201 where a refusal is expected) |
| Lock the source row first instead of the lower warehouse id | Opposing directions (deadlocks surface as 503s) |
| Interpolate a value into a throwaway store query | SQL hygiene source scan |
| Remove the scope fragment from one scoped query | Scope guard source scan |

## 11. What I knowingly left out

These are designed but not built, by choice. **Completed at T21**, which adds anything cut at the 3:20 cut line.

- **PUT and DELETE** for products and warehouses. The assignment's §2 says "CRUD" but §4 lists only list and create; I built §4 (question 10). Delete raises the choice between foreign-key restrict and soft delete.
- **Pagination.** Lists return everything, as "list all" asks. Designed: keyset pagination on `code` (`?after=<code>&limit=<n>`, default 50, max 200), stable under inserts.
- **Warehouse link administration.** Links come from the seed and from auto-linking a warehouse's creator. An admin link endpoint is designed and deferred.
- **Idempotency keys.** A client that retries `POST /orders` after a timeout could transfer twice. Designed fix: an `Idempotency-Key` header stored with a unique constraint on the transfer order.
- **Row-level security** in PostgreSQL, as defence in depth behind the scope join.
- **Roles, refresh tokens and token revocation.** One signing key, 60-minute tokens, no refresh.
- **Transactional outbox** for a `StockTransferred` event. Designed for the interview, not built.
- **CI**, static analysis and dependency scanning (CI is an optional D10 item).
- **Metrics export (OpenTelemetry) and alerting** on 5xx rate, `concurrency_conflict` rate and health failures. Designed only.
- **Backups and disaster recovery:** daily `pg_dump`, RPO 24 h, RTO 1 h by restore. Designed only.
- **Production schema delivery:** a migration job and a least-privilege database role instead of runtime DDL.
- **TLS termination** at a reverse proxy in a real deployment.
- **Read replicas, partitioning of `transfer_orders`, caching.** Not needed at the assumed load.
- **The UI in Compose.** Compose is API + database, as written (question 6).
- **Persistent UI login.** The token lives in memory and is lost on reload.
- **`GET /orders`** is an optional D10 item and is built only if time allows.

## 12. Assumptions and questions asked of SCAD

Logistics questions (tier, timing, preparation before the clock, starter repository, repository access) were answered by SCAD before the build.

Specification questions sent to SCAD on 2026-09-24. Until an answer arrives, the build uses the default shown:

| # | Question | Default taken | Answer |
|---|---|---|---|
| 1 | Is `POST /stock` additive (receiving goods), or does it set the level? | Additive | Pending |
| 2 | `GET /stock` with both `productCode` and `warehouseCode`: only rows matching both? With neither: 400? | Intersection; 400 | Pending |
| 3a | May a user be linked to more than one warehouse? | Yes, one or more | Pending |
| 3b | May a user transfer stock into a warehouse they are not linked to (the source must be theirs)? | Yes | Pending |
| 3c | Is the product list visible to every user, with only stock scoped? | Yes | Pending |
| 4 | Are product and warehouse codes case-sensitive? | Yes, stored as entered after trimming | Pending |
| 5 | Are update and delete for products and warehouses expected (§2 "CRUD"), or is §4 the full surface? | §4 is the surface; update/delete listed under "left out" | Pending |
| 6 | Should Docker Compose run only the API and the database, or the UI as well? | API + database | Pending |
| 7 | Expected volume: total and daily active users, read-to-write ratio, data size (products, warehouses, transfers per day)? Decides read-heavy vs write-heavy | ~100 daily users; 50 warehouses; 10,000 products; 1,000 transfers/day; peak 20 reads/s and 2 writes/s (≈10:1, read-heavy); under 100 MB in year one `[ASSUMED]` | Pending |

Other assumptions:

- Codes are trimmed, 1–50 characters, letters, digits, `.`, `_` and `-`, and case-sensitive `[ASSUMED]`.
- `POST /stock` is additive `[ASSUMED]`, rejects unknown codes, and rejects a quantity of 0 or less.
- `GET /stock` needs at least one filter (else 400). Both filters mean the intersection. An unknown code returns 404. A drained row shows 0.
- A source warehouse with no stock row for the product has 0 available, so the transfer is refused with `insufficient_stock`, not 404.
- Products are a global catalogue; warehouses, stock and transfers are scoped. A transfer's destination may be any existing warehouse. Links come from the seed and from creator auto-linking.
- Load: 50 warehouses, 10,000 products, 20 reads/s and 2 writes/s at peak, p95 under 200 ms `[ASSUMED]`. One API instance and one database carry this.
- .NET 10 is the current LTS and the password hasher ships in the shared framework `[UNVERIFIED — confirm with dotnet --list-sdks and a build on the day]`.
- The grader has Docker and the .NET 10 SDK. `global.json` pins the SDK.
- No starter repository has been received.

## 13. AI usage

SCAD allows and expects AI assistants, provided I can explain and defend every line and say which parts were generated (H8).

- **Before the clock:** this README, `CLAUDE.md` and the ADRs in `docs/decisions/` were drafted with Claude Code (Anthropic) from my own design notes (specification, constitution, system design, concurrency design, task list). Those design notes were also written in AI-assisted sessions; two of them are copied into the repo as `docs/spec.md` and `docs/constitution.md`. I review these documents and must be able to explain every line of them.
- **During the build:** an AI assistant drafts code under explicit task instructions and never commits unreviewed work. Each task ends with an explain-back checkpoint. Independent review passes (code review, security review, verification) run before the pre-submit gate.
- **Commit footer** on every AI-assisted commit: `AI-assisted: <tool> — reviewed and explained`.

Ledger, one row per AI-assisted commit or file. **Completed at T22.**

| Commit or file | Generated by | What I changed | Can I explain every line? |
|---|---|---|---|
| `README.md`, `CLAUDE.md`, `docs/decisions/*.md`, `docs/spec.md`, `docs/constitution.md` (pre-clock) | Claude Code | | |

## 14. Time log

**Completed at T22.** The four-hour cap is hard (H5).

| Field | Value |
|---|---|
| Pre-clock preparation | Design notes; this README draft, `CLAUDE.md`, `docs/decisions/`, `docs/spec.md` and `docs/constitution.md`; cache warming (SDK, NuGet, Docker images, Node) per question 7 |
| Clock start | |
| Authentication green at the 3:20 cut line? | |
| Cuts made (drop order, bottom first) | |
| Clock end | |
| Total elapsed | |

Planned budget:

| Clock | Drop item | Tasks |
|---|---|---|
| 0:00–0:30 | Setup: scaffold, hooks, schema and initializer, fixture, error contract | T00–T03 |
| 0:30–1:25 | D1 transfer, edge cases and tests | T04–T08 |
| 1:25–1:40 | D2 validation and errors | T09 |
| 1:40–1:55 | D3 concurrency | T10–T11 |
| 1:55–2:10 | D4 integration coverage | T12 |
| 2:10–2:40 | D6 UI list and detail; D7 UI create form | T13–T14 |
| 2:40–3:10 | D8 authentication and scoping | T15–T18 |
| 3:10–3:20 | D9 Compose | T19 |
| 3:20–3:30 | D10 e2e spec | T20 |
| 3:30–3:45 | D5 README final pass | T21 |
| 3:45–4:00 | Buffer and pre-submit gate | T22 |
