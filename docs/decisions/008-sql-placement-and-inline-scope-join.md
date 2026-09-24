# ADR-008: SQL placement and the inline warehouse scope join

- Status: Accepted
- Date: 2026-09-24
- Deciders: Thulani Maseko
- Traces: H3, T13, G21, G36, G39; FR-012, FR-013, FR-014; constitution III, VI
- Amends: ADR-001 (where SQL may live), ADR-002 (no `Shared/Health/`), ADR-005 (who sets the demo passwords); supersedes the scope-fragment clause of ADR-006

## Context

- H3 and constitution III: SQL is hand-written with named parameters, never interpolated, formatted or concatenated, and lives in Store files.
- ADR-006 applied the scope through one shared link-join fragment (`ScopeSql`). Reusing a fragment means building each query from pieces, by concatenation or by interpolating a constant. Either breaks the literal "never concatenated" rule that `SqlHygieneTests` enforces, and a source scan cannot tell a safe constant from an unsafe value.
- Two pieces of database plumbing need SQL outside a feature store: the schema initializer's advisory lock (ADR-005) and the health check. The health check needs a real round trip, because opening a pooled connection does not prove the server is reachable.
- ADR-005 had the schema initializer set the demo users' password hashes. That is an `UPDATE users` statement inside plumbing, and authentication logic outside the Auth slice.

## Decision

- **Inline scope join.** Every scoped query in `WarehouseStore`, `StockStore` and `TransferStore` carries the same inner join, written out in full: `JOIN user_warehouses uw ON uw.warehouse_id = w.id AND uw.user_id = @UserId`. There is no shared fragment: each `*Sql` constant is one complete statement. A query that must match either end of a transfer (`GET /orders`, if built) uses `EXISTS (SELECT 1 FROM user_warehouses … AND uw.user_id = @UserId)`.
- **Where SQL may live.** In C# source, SQL appears only in `Features/*/*Store.cs` and `Shared/Persistence/`. `Shared/Persistence/` holds the data source, `SchemaInitializer` (advisory lock, then `schema.sql` and `seed.sql`) and `DatabaseHealthCheck` (one round trip). There is no `Shared/Health/`. The no-interpolation, no-concatenation rule applies in both places.
- **Demo passwords.** `UserStore` sets the demo users' password hashes at startup from `DemoUsers__Password`. `SchemaInitializer` only applies the two files.

## Constraints this decision must honour

- Named parameters only. Every `*Sql` field is `const`, and stores pass only `*Sql` constants to Dapper (H3).
- An unknown and an unlinked warehouse code get identical responses (G39, FR-014). A transfer's destination stays unscoped (G36).

## Consequences

- Positive: each query reads as the SQL that runs. "No concatenated SQL" stays literally true, so the hygiene scan needs no exception for fragments.
- Positive: the scope rule stays visible in every scoped query the assessor reads.
- Negative: the join is repeated in about eight queries. A change to the link model means editing each one; the scope guard finds any that are missed.
- Cost accepted: SQL may live in two places instead of one. The plumbing SQL is small and holds no business rule.

## Alternatives considered

- **Keep `ScopeSql` and concatenate it.** Breaks H3's "no concatenated SQL" as written, and the hygiene scan would need an exception.
- **A view or function such as `visible_warehouses(@UserId)`.** Hides the predicate from the store reader and moves logic into the schema. Worth revisiting if the join grows.
- **Row-level security.** Deferred defence in depth (ADR-006).
- **Calling the plumbing a store.** Naming the schema initializer `*Store` would meet the letter of the rule and mislead the reader: it runs script files, not feature queries.

## How we prove it

- Behaviour first (T16): alice cannot see WH-B in the warehouse list or in stock queries; WH-B and an unknown code get identical responses; alice cannot transfer out of WH-B but can transfer into it.
- Scope guard (T17, reflection over the stores): every `*Sql` field is `const`; every one that reads `warehouses`, `stock` or `transfer_orders` contains an inner `JOIN user_warehouses` or an `EXISTS (… user_warehouses …)`, and `@UserId`; none uses `LEFT JOIN user_warehouses`. Mutation: remove the join from one constant, and the test goes red.
- SQL hygiene (T01): SQL keywords appear only in `Features/*/*Store.cs` and `Shared/Persistence/`, and neither place interpolates, formats or concatenates SQL.
- Demo login (T15): with `DemoUsers__Password` set, alice can log in; without it, login fails and a warning is logged.

## Revisit when

- The link model changes (roles, tenants): consider a view, a function or row-level security.
- More plumbing needs SQL: keep it in `Shared/Persistence/`, or give it a store of its own.
