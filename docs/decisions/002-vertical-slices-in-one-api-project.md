# ADR-002: Vertical slices in one API project

- Status: Accepted. Amended by [ADR-008](008-sql-placement-and-inline-scope-join.md): no `Shared/Health/` (the health check lives in `Shared/Persistence/`), and the scope guard checks the inline join instead of a fragment
- Date: 2026-09-24
- Deciders: Thulani Maseko
- Traces: B1, B4, B5, H5, H10, S4, A2, A4, I2, G16; constitution II and the "Layout" standard

## Context

- The API has five feature areas: Auth, Products, Warehouses, Stock and Orders. That is about twenty low-complexity function points plus one high-complexity concern, the transfer's concurrency.
- A2: SCAD's house pattern is "one endpoint, one Dapper query, one integration test, one UI page".
- B1: any .NET approach is acceptable. B4: dependency injection, separation of concerns, async where it earns its keep.
- H10 and constitution II: small, correct and genuinely tested beats large and unproven. No abstraction enters without a second caller or a test that needs the seam.
- My coding standards propose a four-project layered template. Under the four-hour clock (H5) every extra project and file is a cost.
- Unit tests must run in seconds inside the pre-commit hook. Integration tests need Docker.

## Decision

We build one ASP.NET Core Minimal API project, with one endpoint-group file per feature:

```text
src/Inventory.Api/
  Program.cs     composition root only
  Features/      Auth, Products, Warehouses, Stock, Orders
                 (endpoints, contracts, validator, store per slice)
  Shared/        Persistence, Errors, Auth, Validation, Health
tests/Inventory.UnitTests/
tests/Inventory.IntegrationTests/
```

- Endpoints call stores directly where there is no decision logic. `TransferService` exists because the transfer has outcomes to interpret.
- Interfaces exist only where a test needs a seam: `ITransferStore` (faked in the `TransferService` unit tests) and `ICurrentUser` (keeps HTTP types out of stores). The only others are framework-required (exception handler, health check).
- DI lifetimes: singleton for the Npgsql data source, validators, exception handler, token issuer, password hasher and options. Scoped for the current user, all stores and `TransferService`. Scope validation and build-time validation are on.
- Two test projects: `Inventory.UnitTests` (no Docker) and `Inventory.IntegrationTests` (Testcontainers).
- Slices do not call each other. They share only `Shared/` and the database.

## Constraints this decision must honour

- SQL only in Store files (H3, constitution III): each slice has one obvious place for its SQL.
- Constructor injection only; no `new` of a collaborator inside a class (B4).
- Explicit types (no `var`), `sealed` by default, records for contracts, file-scoped namespaces, one public type per file, warnings as errors. Enforced by `.editorconfig` and `dotnet format` before every commit.
- Methods under about 20 lines, classes under about 200.
- Project references do not enforce the layering, so source-scan tests do.

## Consequences

- Positive: a change to one feature happens in one folder. The layout matches SCAD's house pattern. Fewer files to write and explain in four hours.
- Positive: no pass-through services. That answers "why no ProductService?": it would be a layer with nothing to test.
- Negative: this is a documented deviation from the four-project template in my coding standards.
- Cost accepted: less compile-time enforcement of layering, covered by source-scan tests.

## Alternatives considered

- **Four-project layered solution (Clean Architecture).** Rejected for five endpoints: more projects and files, with nothing yet to decouple. The split trigger below says when it becomes worth it.
- **Microservices.** One team, one database, five endpoints. Splitting would turn a transfer that needs one local transaction into a distributed one.
- **Pass-through services for Products, Warehouses and Stock.** A layer with nothing to test (YAGNI).
- **Mediator or CQRS library.** No second consumer, so the indirection decouples nothing.
- **Generic repository or Unit of Work abstraction.** Hides the SQL. Only one multi-statement transaction exists, and its store owns it.
- **One test project.** Would force Docker into every commit's pre-commit run.

## How we prove it

- Every integration test boots the real composition root, so DI registrations and lifetimes are exercised. Scope validation fails at startup if a singleton captures a scoped service.
- `TransferServiceTests` use a hand-written fake `ITransferStore`: "a successful outcome is returned as the transfer order" and "an insufficient outcome raises InsufficientStockException whose message names product, source, requested and available". This is the seam earning its place, and it covers the business-logic unit tests B5 asks for.
- `SqlHygieneTests` and the scope guard test (T17) are source scans that keep SQL and the scope fragment where the layout says they are.

## Revisit when

- A second consumer of the application logic appears, such as an outbox relay worker (interview prompt I2) or a CLI importer. Then move to a multi-project layered solution.
- A starter repository arrives with a different structure (A4): follow it and record the objection in the README.
