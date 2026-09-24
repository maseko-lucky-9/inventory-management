# ADR-005: Schema applied idempotently at startup

- Status: Accepted. Amended by [ADR-008](008-sql-placement-and-inline-scope-join.md): `UserStore`, not `SchemaInitializer`, sets the demo users' password hashes
- Amended after the four-hour build (2026-09-24): when `Seed:DemoData` is true (off by default; `SEED_DEMO_DATA` in Compose), `SchemaInitializer` also applies `db/demo-data.sql` after `seed.sql`, in the same transaction under the same advisory lock.
- Date: 2026-09-24
- Deciders: Thulani Maseko
- Traces: H2, H3, H4, H9, T11, G3, G20, G22, G23, G24, G34, R8; FR-015, FR-016, FR-018

## Context

- H4: we design the schema (products, warehouses, stock per product per warehouse) and ship the DDL as a `.sql` file or migration, so the database can be created from the repo without asking.
- H9: "If we cannot run it, we cannot score it."
- G3: a grader who uses the assignment's `docker run` one-liner gets an empty database. Container init scripts only run through Compose, so initdb-only DDL would never run.
- The integration suite needs the same schema on a fresh container every run (G20).
- Two API instances may start at the same moment.
- H3 bans ORMs, so ORM migrations are not available.

## Decision

- `db/schema.sql` holds the tables, constraints and indexes. Every statement is idempotent.
- `db/seed.sql` holds only demo data: warehouses `WH-A`, `WH-B`, `WH-C`; users `alice`, `bob`, `carol` with no password hash; and their links (alice to WH-A, bob to WH-B, carol none). Every insert is insert-if-absent.
- `SchemaInitializer.ApplyAsync(NpgsqlDataSource, string contentRoot, CancellationToken)` runs at startup. It takes a PostgreSQL advisory lock, applies `schema.sql`, then `seed.sql`, then sets the demo users' password hash from `DemoUsers__Password` (environment, read from the local `.env`) using the framework hasher. If that setting is absent, it logs a warning and the demo users cannot log in.
- One path for every way of running: `dotnet run` against the one-liner's database, Compose, and the integration fixture (`PostgresFixture` plus `ApiFactory`).
- The default connection string equals the one-liner's values. Compose passes its connection string by environment and starts the API only after PostgreSQL is healthy (G23).
- The README documents a manual `psql -f` fallback.
- The database assigns timestamps; no time-provider abstraction (G34).

## Constraints this decision must honour

- Schema shipped in the repo as the single source of truth (H4).
- PostgreSQL only: `postgres:17-alpine` in Compose and in the tests (H2).
- No ORM migration tooling (H3).
- No secrets in git (G22): no password or hash is committed. The demo password lives only in the local, gitignored `.env`; the committed `.env.example` holds `<PLACEHOLDER>` values only.

## Consequences

- Positive: SCAD's own `docker run`, Compose and the test suite all build the database the same way. Nothing has to be run by hand.
- Positive: the advisory lock serialises two instances that start together.
- Cost accepted: the app's database role needs DDL rights at runtime. Fine for a test; production would use a migration job.
- Negative: idempotent create-if-missing DDL does not migrate existing data.

## Alternatives considered

- **Container init scripts only.** They run only with Compose. A grader using the one-liner would get an empty database.
- **A migration library.** Rejected because the startup initializer already serves the one-liner, Compose and the tests through one path.
- **Manual `psql`.** Requires the grader to act, which H4 rules out. Kept only as the documented fallback.
- **EF Core migrations.** Banned together with the ORM (H3).
- **Pre-computed demo password hashes in `seed.sql`.** The grader still needs the plain value to log in, so it would have to be committed somewhere (the README or `.env.example`). Hashing a value from `.env` at startup keeps every password out of git; the cost is one README step, `cp .env.example .env`.

## How we prove it

- "a fresh database gets every table after startup" (integration, fresh container).
- "applying the schema twice raises no error": H4 idempotent delivery. It does not prove migration of existing data.
- "health reports healthy when the database is reachable".
- "a demo user can log in with the configured demo password"; "with no demo password configured, demo login returns 401 invalid_credentials". The fixture sets a random value per run.
- Pre-submit fresh-clone smoke test: clone into a temp directory and follow the README verbatim, both quick-start options (G24).
- Target: a fresh clone reaches a healthy API through `docker compose up` in under two minutes.

## Revisit when

- The service moves to production: run the schema as a migration job, and give the app a least-privilege database role (designed-deferred).
- A schema change must migrate existing data, which create-if-missing DDL cannot do.
