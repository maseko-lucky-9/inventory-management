# ADR-001: PostgreSQL with Dapper, no ORM

- Status: Accepted. Amended by [ADR-008](008-sql-placement-and-inline-scope-join.md): SQL may also live in `Shared/Persistence/` (schema initializer, health check)
- Date: 2026-09-24
- Deciders: Thulani Maseko
- Traces: H2, H3, T3, B4, B6, C1, C2, C3, A2, I5, G4, G16; constitution I, III, IV; FR-022

## Context

- H2: the database is PostgreSQL. Not SQLite, not SQL Server, not in-memory. The assignment gives a `docker run` one-liner for `postgres:17-alpine`.
- H3: no ORM. Dapper or raw ADO.NET with parameterised SQL. Entity Framework Core, NHibernate and ServiceStack OrmLite are explicitly not accepted.
- G4: Dapper add-ons that generate SQL (Contrib, SimpleCRUD, FastCRUD) and query builders pass "no ORM" in letter but not in spirit.
- C1–C3: the transfer needs a guarded `UPDATE` and an upsert written exactly as intended. That SQL must be visible and reviewable.
- A2: SCAD's house pattern is "one endpoint, one Dapper query, one integration test, one UI page".
- The house coding standard says "no raw SQL in application code". The assignment bans the ORM that standard assumes, so the two must be reconciled.

## Decision

We use PostgreSQL 17 (`postgres:17-alpine`) everywhere: in Compose, in the Testcontainers fixture, and as the target of the default connection string, which equals the one-liner's values.

We reach it through a singleton Npgsql data source (the connection pool) and Dapper. Dapper is a mapper: it maps rows to records and generates no SQL.

Every SQL statement is a constant in a feature's `*Store` file (`ProductStore`, `WarehouseStore`, `StockStore`, `TransferStore`, `UserStore`). Parameters are always named parameters. Nothing outside a Store file contains SQL. The store is the data layer, which satisfies "no raw SQL in application code" (constitution III).

The cancellation token flows from the request into every Dapper command. Async is used for I/O only (B4, G16).

## Constraints this decision must honour

- PostgreSQL only (H2): `scripts/check-constraints.sh` fails if any project file references `Sqlite`, `SqlClient` or `InMemory`. It runs in the pre-commit hook and at the pre-submit gate.
- No ORM and no SQL-generating helper (H3, G4): the same scan denies `EntityFrameworkCore`, `NHibernate`, `ServiceStack`, `OrmLite`, `Dapper.Contrib`, `Dapper.SimpleCRUD`, `Dapper.FastCrud`, `SqlKata`, `linq2db` and `RepoDb`.
- Parameterised SQL in one place (H3): `SqlHygieneTests` fails if a Store file interpolates, formats or concatenates SQL, or if any other source file contains SQL keywords. It runs with every unit run.
- Invariants that must hold under concurrency live in the database (constitution I): CHECK, unique and foreign-key constraints, and the guarded decrement.
- Tests use the same database as the grader. No in-memory provider, even in tests.

## Consequences

- Positive: the SQL a reviewer wants to read sits in one obvious file per feature. Guarded updates, upserts and advisory locks are written directly.
- Positive: Dapper removes the row-mapping boilerplate of raw ADO.NET.
- Negative: SQL is not checked at compile time. Anything involving SQL, constraints or transactions must be proven by integration tests against a real PostgreSQL (constitution IV, B6).
- Negative: records are mapped by hand, a line each. No object mapper library.
- Cost accepted: one dependency (Dapper) over raw ADO.NET.

## Alternatives considered

- **Raw ADO.NET.** Allowed by H3 and gives the same hand-written SQL. Rejected for the extra mapping code under a four-hour clock. Dapper is also SCAD's own stack.
- **Entity Framework Core, NHibernate, ServiceStack OrmLite.** Banned by H3. For the interview (I5): the case for a ban is hidden generated SQL, change-tracker surprises, and locking semantics that need raw SQL anyway. The case against is CRUD productivity, typed LINQ and built-in migrations.
- **Dapper.Contrib, Dapper.SimpleCRUD, Dapper.FastCrud, SqlKata, linq2db, RepoDb.** They generate SQL. Banned to keep the spirit of H3 (G4).
- **SQLite or an in-memory provider for tests.** Banned by H2. It would also not prove PostgreSQL locking or constraint behaviour.
- **A generic repository.** It hides exactly the SQL the assessor wants to read.

## How we prove it

- `SqlHygieneTests`: "store files contain no interpolated, formatted or concatenated SQL, and no other source file contains SQL keywords". Mutation check (T01): add a throwaway store file with an interpolated query; the test must go red; then delete the file.
- `scripts/check-constraints.sh` passes on every commit and at pre-submit.
- The integration suite runs through the real HTTP path against `postgres:17-alpine` (B6). For example, "posting a product twice returns 201 then 409 whose detail names the code" proves the unique constraint, the translation and the handler together.

## Revisit when

- A starter repository arrives (A1–A4). Its data-access pattern is followed even where it differs, and the objection goes in the README.
- The code leaves this assessment for a team without the H2/H3 constraints. Then the I5 synthesis applies: constrain rather than ban, with EF for simple CRUD and hand-written SQL on hot or subtle paths, and only for a SQL-fluent team that reviews the generated SQL.
