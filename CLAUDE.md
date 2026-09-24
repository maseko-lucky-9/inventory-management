# CLAUDE.md — inventory-management

> Warehouse inventory API: products, warehouses, stock levels, and atomic stock transfers between warehouses that never oversell under concurrency. Plus a small UI. Built as the SCAD Software .NET skills test (August 2026), Senior tier, inside a four-hour hard cap.

This file sets the rules for any AI assistant working in this repo. The rules come from the assignment and from the design set written before the clock started (in the author's notes: `wiki/career/project/scad-skills-test/`; the spec and constitution are copied into `docs/`, the rest stays outside this repo). Where this file and the assignment disagree, the assignment wins (see Precedence).

## Status

Pre-build. Only `CLAUDE.md`, `README.md`, `.env.example`, `docs/spec.md`, `docs/constitution.md` and `docs/decisions/` exist. **Do not scaffold, write code or run `git init` until the user says the clock has started.** Task T00 is the first thing on the clock.

## Stack

| Concern | Choice |
|---|---|
| Runtime | C# on .NET 10 LTS `[verify: dotnet --list-sdks]`, pinned by `global.json` (roll forward to the latest feature band) |
| API | ASP.NET Core Minimal APIs, one endpoint-group file per feature; paths exactly as the assignment gives them (no `/api` prefix) |
| Data | PostgreSQL 17 (`postgres:17-alpine`) via `NpgsqlDataSource` + Dapper; hand-written, parameterised SQL |
| Validation | FluentValidation through an endpoint filter |
| Errors | Domain exceptions → one `IExceptionHandler` → RFC 9457 Problem Details with `code` and `traceId` |
| Auth | JWT bearer (framework JwtBearer handler), framework password hasher; warehouse scoping via `user_warehouses` inside store queries |
| Tests | xUnit; Testcontainers (`postgres:17-alpine`) + `WebApplicationFactory<Program>`; Playwright for one e2e spec |
| UI | Vue 3 + Vite + TypeScript, Vue Router, fetch wrapper; Vite dev proxy (same origin, no CORS) |
| Packaging | Multi-stage Dockerfile; Docker Compose (API + Postgres, health-gated) |

## Capacity baseline (working assumption `[ASSUMED]`)

The assignment gives no volume figures. Question 7 of the email to SCAD asks for them. Until they answer, the build uses this baseline (user ruling, 2026-09-24):

| Figure | Value |
|---|---|
| Users | About 100 daily users |
| Data | 50 warehouses, 10,000 products, up to 500,000 stock rows |
| Writes | About 1,000 transfers a day |
| Peak traffic | About 20 reads/s and 2 writes/s: roughly 10 reads to every write, so **read-heavy** |
| Storage | Well under 100 MB in the first year (stock ~50 MB, transfer history ~40 MB a year) |
| Latency target | p95 under 200 ms per endpoint |

What it means for the code:

- One API instance and one PostgreSQL instance. Don't add a cache, read replica, queue, partitioning or load balancer; none is needed at this load, and the README lists each as left out.
- Every read path is index-backed:
  - unique indexes on `products.code`, `warehouses.code` and `users.username`;
  - stock primary key (`product_id`, `warehouse_id`) plus a secondary index on `stock.warehouse_id`;
  - link primary key plus a secondary index on `user_warehouses.warehouse_id`;
  - `transfer_orders(created_at)`.

  A new read query ships with the index it uses.
- Writes are rare, so the guarded UPDATE's row lock isn't a bottleneck. Its ceiling is about 200–500 transfers a second on one (product, source) pair.
- The Npgsql pool is capped at 50 per instance.
- Revisit if SCAD's answer is write-heavy, or if one item's transfer rate approaches that ceiling. Revisit ADR-003 before adding infrastructure.

## Layout (built up from T00 to T20)

```text
.env.example                          committed; placeholders only. Copy to .env (gitignored) for local runs
.githooks/{commit-msg,pre-commit}     format + scope check; build + unit tests + constraint scan
db/{schema.sql,seed.sql}              the only schema source; idempotent; applied at startup
scripts/check-constraints.sh          ORM / non-Postgres package deny-list
src/Inventory.Api/
  Program.cs                          composition root only
  Features/{Auth,Products,Warehouses,Stock,Orders}/   Endpoints · Contracts · Validator · Service · Store
  Shared/{Persistence,Errors,Auth,Validation,Health}/
tests/Inventory.UnitTests/            validators, TransferService (fake store), DbErrorTranslator, source scans
tests/Inventory.IntegrationTests/     Postgres fixture, HTTP tests, concurrency, authorization
ui/                                   Vue 3 + Vite
e2e/                                  Playwright spec
docs/decisions/                       ADR-001 … ADR-007
docs/{spec,constitution}.md           requirements FR-001 … FR-023; principles I … VIII
```

Dependency direction inside a slice: Endpoints → Service → Store. Never backwards.

## Commands (valid once the matching task has run)

```bash
cp .env.example .env                           # once per clone; replace each <PLACEHOLDER>
set -a; . ./.env; set +a                       # before dotnet run (compose reads .env itself)
dotnet build                                   # T00; warnings are errors
dotnet test tests/Inventory.UnitTests          # seconds; run by the pre-commit hook
dotnet test                                    # unit + integration; needs Docker running
dotnet format --verify-no-changes              # before every commit
scripts/check-constraints.sh                   # T01; must print nothing and exit 0
docker compose up --build                      # T19; API + postgres:17-alpine
```

## Hard constraints — never break these (assignment §3)

| ID | Rule | Gate that enforces it |
|---|---|---|
| H2 | PostgreSQL only, everywhere including tests. No SQLite, SQL Server or in-memory provider | `scripts/check-constraints.sh` (pre-commit + pre-submit) |
| H3 | No ORM and no SQL-generating helper. Dapper or raw ADO.NET only. **Never add:** `EntityFrameworkCore`, `NHibernate`, `ServiceStack`/`OrmLite`, `Dapper.Contrib`, `Dapper.SimpleCRUD`, `Dapper.FastCrud`, `SqlKata`, `linq2db`, `RepoDb`, anything with `Sqlite`, `SqlClient` or `InMemory` | Same deny-list scan |
| H3 | SQL is a constant in a feature's `*Store` file, with **named parameters only**. No interpolated, formatted or concatenated SQL. No SQL keywords in any other file | `SqlHygieneTests` (unit suite, every commit) |
| H4 | The database is creatable from the repo without asking: `db/schema.sql` + `db/seed.sql`, idempotent, applied by `SchemaInitializer` at startup under an advisory lock | Integration suite on a fresh container; fresh-clone smoke test |
| H5 | Four-hour hard cap. Priority is drop order D1 → D10. **Cut line at 3:20:** if auth isn't green, drop the e2e spec, then Compose, and record the cuts in the README | README time log |
| B6 | Integration tests hit a real PostgreSQL through the real HTTP pipeline. A mocked test never stands in for one | Test catalogue in README |

Correctness under concurrency lives in the database (ADR-003): a guarded `UPDATE … WHERE quantity >= @requested`, `CHECK (quantity >= 0)`, one READ COMMITTED transaction, stock rows locked in ascending warehouse-id order. Don't replace this with application-level locking, `SERIALIZABLE` retries or a version column without a new ADR.

## Commit hygiene — never break these (assignment §10)

- Subject must match, and be at most 72 characters:
  `^(feat|fix|test|docs|chore|refactor|build|ci)\((repo|api|db|products|warehouses|stock|orders|auth|ui|e2e|compose|readme|tests)\): [a-z].{8,}$`
- One task = one logical change = one commit, with the message pre-written in the task list (T00–T22). Never "wip", "final" or a bare "fix". Never one commit containing everything.
- Never commit a red build or red unit tests; never bypass hooks (`--no-verify` is forbidden).
- **Never squash.** A squash merge produces exactly the history the assignment forbids. The global push guard blocks direct pushes to `main`, so work on a `work` branch and merge with `gh pr merge --rebase` (or fast-forward) only.
- Commits drafted with AI carry the footer `AI-assisted: <tool> — reviewed and explained`, and the README AI section lists them.
- Pre-submit: `git log --format=%s` through the same regex prints nothing; `git log --merges` is empty.

## Coding standards

- Explicit types, no `var`. `sealed` by default. Records for requests, responses and read models. File-scoped namespaces. One public type per file. Warnings as errors.
- Constructor injection only; no `new` of a collaborator. Lifetimes chosen deliberately; scope validation on.
- Interfaces only where a test needs a seam: `ITransferStore` and `ICurrentUser`. No others without a complexity-tracking entry.
- Async for I/O only; the `CancellationToken` flows from the request into every Dapper `CommandDefinition`. No `.Result`, `.Wait()` or `async void`.
- Methods under ~20 lines, classes under ~200.
- YAGNI is a scoring strategy here: no feature without its proving test; no abstraction without a second caller or a test seam.

## Error contract (ADR-004)

Every non-2xx response is Problem Details with `code` and `traceId` (plus `errors` for validation). Key codes:

| Status | `code` |
|---|---|
| 400 | `malformed_request`, `validation_failed`, `unknown_product_code`, `unknown_warehouse_code`, `insufficient_stock`, `quantity_out_of_range` |
| 401 | `unauthorized`, `invalid_credentials` |
| 404 | `product_not_found`, `warehouse_not_found` (unknown code in a path or query) |
| 409 | `duplicate_product_code`, `duplicate_warehouse_code` |
| 429 | `too_many_requests` (login only) |
| 503 | `concurrency_conflict` (+ Retry-After), `database_unavailable` |
| 500 | `internal_error`: no stack trace; a CHECK violation is logged as a defect |

An unknown **or unlinked** warehouse code gets the same response as an unknown one, so scope never leaks existence. The one exception is a transfer's destination: it may be any existing warehouse.

## Security

- Authentication at the boundary; every route except `POST /auth/login` and `/health` requires a token.
- Authorization inside store queries: every scoped query joins `user_warehouses` on the current user's id. The UI never enforces permissions.
- Secrets come from the environment, never source. Real values live only in the local `.env`, which is gitignored (T00's `.gitignore` must list it). `.env.example` is committed with `<PLACEHOLDER>` values only; when a new setting is needed, add it there with a comment. **Never write a real password, key or token into any committed file.** The one exception is the assignment's own public `postgres` value from its §3.1 one-liner.
- Demo users (alice → WH-A, bob → WH-B, carol → no links) are demo-only. `db/seed.sql` creates them with no password hash; `SchemaInitializer` hashes `DemoUsers__Password` from the environment at startup. If it's unset, they cannot log in and a warning is logged. The integration fixture sets its own random value per run.

## Glossary (use these names exactly)

Product (`products`, `/products`) · Warehouse (`warehouses`, `/warehouses`) · Stock (`stock`, `/stock`, `StockLevel`) · Transfer order (`transfer_orders`, `/orders`, `TransferOrder`) · User (`users`, `/auth/login`) · Link (`user_warehouses`) · Scope (the set of warehouses linked to the current user)

## Precedence (higher wins)

1. A starter repository's README, if SCAD sends one
2. The assignment's hard constraints and scored requirements
3. A starter repository's existing code pattern: follow it, and put objections in the README
4. The ADRs in `docs/decisions/` and the design set
5. Personal coding standards

## How to work here

- Before a task: confirm its proving test and commit message from the task list. Write the failing test first.
- After a task: the proving test is green, `dotnet format` is clean, and the user can explain every line (explain-back). Then commit.
- A change that contradicts an ADR needs a new ADR first. Don't edit an accepted ADR's decision; supersede it.
- Out of scope for the 240 minutes (README "left out"): PUT/DELETE, pagination, idempotency keys, outbox, link administration, row-level security, CI.
