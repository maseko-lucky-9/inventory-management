# ADR-003: Guarded update for concurrent transfers

- Status: Accepted
- Date: 2026-09-24
- Deciders: Thulani Maseko
- Traces: C1, C2, C3, C4, E10, E11, E14, T4, H2, H3, B6, S1, S2, I1, I2, G7, G8, G19, G31; FR-005, FR-006, FR-007, FR-009; constitution I

## Context

- C1: two transfers of the same product out of the same warehouse, arriving at the same instant, must never oversell it.
- C2: reading the quantity, checking it in application code and writing it back is not sufficient. It is the classic lost-update (check-then-act) race.
- C3: pick one strategy: row locking, a conditional UPDATE with a guard predicate, a stricter isolation level, or an optimistic version column. C4: implement it and say in the README what it costs.
- E11: the decrement and the increment happen atomically. E14: order history stays cheap, so it is one row in the same transaction.
- The API is stateless and may run as several instances. The only thing every writer shares is the database.
- S1 and S2 together are 45 of the 100 points.

## Decision

A guarded decrement inside one READ COMMITTED transaction, backed by a non-negative CHECK constraint, with rows touched in ascending warehouse-id order. On one pooled connection, `TransferStore`:

1. Opens one transaction at the default READ COMMITTED level.
2. Resolves the codes in one query. The source must be linked to the caller; the destination may be any existing warehouse. Anything missing: roll back, 400 naming the code.
3. Touches the lower warehouse id first. Source: decrement only where `quantity >= requested`. Destination: upsert (insert, or add if the row exists).
4. If the guard matched zero rows (no stock row counts as available 0, G7), reads the current quantity for the message, rolls back, and returns the insufficient outcome. `TransferService` raises `InsufficientStockException`: 400 `insufficient_stock` naming product, source, requested and available.
5. Inserts the `transfer_orders` row and commits.

Why it is correct: PostgreSQL's documentation (§13.2.1, Read Committed) says an `UPDATE` that finds its row locked waits, then re-evaluates its WHERE clause against the updated version of the row. The guard is always checked against the committed value.

Supporting rules. `CHECK (quantity >= 0)` is defence in depth; if it fires, it is a defect (500 `internal_error`, error log), not a refusal. Deadlock, serialization failure or lock timeout become 503 `concurrency_conflict` with Retry-After. `lock_timeout` is set per session on the transfer path; `statement_timeout` is on the connection string. Destination overflow is 400 `quantity_out_of_range` and rolls back (G8).

## Constraints this decision must honour

- No application-level read-check-write (C2): the guard lives in the WHERE clause, never in a C# comparison.
- The database enforces correctness under concurrency (constitution I).
- SQL only in `TransferStore`, with named parameters (H3). Proven on `postgres:17-alpine` through real HTTP (H2, B6).

## Consequences

- Positive: one statement decides. No retry loop. Correctness does not depend on how many API instances run.
- Cost: transfers of one product out of one warehouse queue for one transaction each, so a hot item is limited to roughly one transfer per transaction latency (about 200–500 per second `[ASSUMED]`).
- Cost: waiting requests hold pooled connections, so a very hot item could exhaust the pool (mitigated by `lock_timeout` and `statement_timeout`).
- Cost: the "available" figure comes from a follow-up read and can already be stale. Part of the rule lives in SQL, so integration tests prove it, not unit tests.
- A client retry after a timeout could transfer twice. The `Idempotency-Key` fix (G31) is designed and deferred.

## Alternatives considered

- **Dekker's algorithm, or a C# `lock`.** Evaluated and rejected. Dekker handles exactly two processes and needs shared memory, but API instances share only the database. It busy-waits, needs memory barriers on modern CPUs, protects only code that runs the protocol (psql or a second service bypass it), and is not crash-safe. It is kept as the lineage: Dekker, Peterson, Lamport's bakery, compare-and-swap, OS mutexes, and finally the database lock manager used here.
- **`SELECT … FOR UPDATE`.** The runner-up. Two round trips, the lock is held longer, and rows still need a fixed lock order. Preferred only if the "available" figure had to match the moment of refusal exactly.
- **SERIALIZABLE or REPEATABLE READ.** Needs a retry loop for serialization failures, aborts some non-conflicting work, lowers throughput, and is the hardest to explain precisely.
- **Optimistic version column.** Two or more round trips and retries, with retry storms on exactly the hot rows.

## How we prove it

- Parallel oversell (integration): seed 10, fire ten transfers of 3 at once. Exactly 3 succeed, 7 get `insufficient_stock`, source ends at 1, destination at 9, three orders exist.
- Deterministic lock interleaving (integration, G19): a raw connection holds the row lock with its own guarded decrement; the HTTP transfer is seen to block; the raw transaction commits; the HTTP call returns 400 with available equal to the committed value.
- Opposing directions (integration): twenty simultaneous A→B and B→A transfers. No deadlock, no 5xx, total quantity conserved.
- Overflow rollback (integration): a transfer that would overflow the destination returns 400 `quantity_out_of_range` and leaves the source unchanged.
- Service mapping (unit, fake `ITransferStore`): the refusal names product, source, requested and available.

| Mutation (run once; each must go red, then be restored) | Expected red test |
|---|---|
| Delete the guard predicate only | Parallel oversell, lock interleaving (CHECK fires: 500, not 400) |
| Delete the guard and the CHECK constraint | Parallel oversell (stock goes negative) |
| Replace the guard with a C# read, compare and unguarded write | Lock interleaving (201 where a refusal is expected) |
| Lock the source row first instead of the lower warehouse id | Opposing directions (deadlocks surface as 503s) |

## Revisit when

- One (product, source) pair needs more than about 200–500 transfers per second: split the counter into buckets, or queue per item.
- The refusal's "available" figure must be exact: switch to `SELECT … FOR UPDATE`.
- Clients retry after timeouts: add the `Idempotency-Key` (G31). The transfer must publish `StockTransferred` (I2): write an outbox row in the same transaction.
