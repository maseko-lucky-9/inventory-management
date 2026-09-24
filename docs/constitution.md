# SCAD Skills Test — Constitution

> Copied into this repo from my pre-clock design notes, with Obsidian formatting removed. Rule IDs such as H4, C1 or G35 come from the simplified spec. That note and the others named here (system design, implementation plan, task list and so on) are not in this repo; the decisions they led to are in [docs/decisions/](decisions/).

> **What this note is**
> The non-negotiables for the build, in the shape of a spec-kit constitution: principles first, then constraints, then workflow gates, then governance. Every later note ([specification](spec.md), the system design, the implementation plan, the task list) is checked against this one. If a task would violate a principle, the task changes, not the principle.

> **Why / what to learn — why a constitution before a plan**
> Spec-kit runs `/speckit-constitution` once per project because principles decided under time pressure drift. Writing them down when there is no clock running means that at 2:40 into the four hours, when a shortcut looks tempting, the decision has already been made. The interviewer's question "why did you structure it that way?" is answered by pointing here.

## Core principles

### I. The database enforces correctness under concurrency
Invariants that must hold across concurrent requests live in the database: the non-negative stock check, the unique codes, the foreign keys, and the guarded decrement. Application code may validate early for a better message, but never *relies* on having checked. Source: the simplified spec C1–C2, S2.

### II. Small, correct and genuinely tested beats large and unproven
SCAD's own reading rule (H10) is the project's scope rule. No feature enters without its proving test. No abstraction enters without a second caller or a test that needs the seam. YAGNI is a scoring strategy here, not a style preference.

### III. SQL is hand-written, parameterised, and lives in one place
Every SQL statement is a constant in a feature's Store file. The one other place SQL may live is the database plumbing in `Shared/Persistence/`: the schema initializer and the health check (ADR-008). Parameters are always named parameters, never string interpolation. Nothing else contains SQL. This satisfies H3 and the house standard "no raw SQL in application code" at the same time: the store *is* the data layer.

### IV. Prove each behaviour at the lowest layer that can prove it
Validation rules → unit tests on the validator. Outcome-to-error mapping → unit tests on the service with a fake store. Anything involving SQL, constraints, transactions, locking, routing, JSON, status codes or auth → integration tests through the real HTTP path against a real PostgreSQL (B6). A mocked test never stands in for an integration test.

### V. One logical change per Conventional Commit
`type(scope): summary`. One task, one commit, one green test suite. Never "wip", never "final", never a squash. The commit history is a document SCAD reads (H6, S7).

### VI. Security is enforced server-side, in the data layer
Authentication at the API boundary; authorization inside the store queries (T13). The UI reflects permissions, it never enforces them. Secrets come from configuration, never from source.

### VII. One error contract
Every non-2xx response is an RFC 9457 Problem Details document with a stable machine-readable `code` and a `traceId`. Expected failures are domain exceptions translated by one global handler; unexpected failures become a 500 without a stack trace (B2).

### VIII. Every line explainable; AI use disclosed
An AI assistant may draft; the candidate reviews, understands and can defend every line (H8). Generated commits carry a footer; the README carries an AI section; each task ends with an explain-back checkpoint.

## Hard constraints (from the assignment, non-negotiable)

| Constraint | Rule | Enforcement gate |
|---|---|---|
| Database | PostgreSQL only — `postgres:17-alpine` in Compose, in Testcontainers and in the README one-liner. No SQLite, SQL Server or in-memory provider anywhere, including tests | Package deny-list scan in `scripts/check-constraints.sh`, run by the pre-commit hook and the pre-submit gate |
| Data access | Dapper over Npgsql (or raw ADO.NET). **Banned:** Entity Framework Core, NHibernate, ServiceStack OrmLite, and any SQL-generating helper — Dapper.Contrib, Dapper.SimpleCRUD, Dapper.FastCrud, SqlKata, linq2db, RepoDb | Same deny-list scan. Dapper is a *mapper* (rows → objects); it generates no SQL, which is the distinction to state at the interview |
| Parameterised SQL | No interpolated, formatted or concatenated SQL. Named parameters only | SQL-hygiene unit test scans every Store and `Shared/Persistence/` source file for interpolation and concatenation next to SQL keywords, and every other file for SQL keywords |
| Schema shipped | `db/schema.sql` and `db/seed.sql` are the single source of truth; applied idempotently by the API at startup and by the integration suite against a fresh container | The integration suite passing on a fresh container is the proof; fresh-clone smoke test before submission |
| Time box | Four hours. Drop order D1–D10 governs what is cut | Start/end recorded in README; tasks carry timeboxes |
| Commit hygiene | Conventional Commits; one logical change per commit | `.githooks/commit-msg` regex; pre-submit history audit; no squash merges |
| README | Setup, run API, run tests, example calls, concurrency paragraph, what was left out | README checklist in the implementation plan |
| Submission | Private repo shared with SCAD or zip; runnable without asking | Fresh-clone smoke test |

## Engineering standards adopted

| Standard | Rule adopted | Source |
|---|---|---|
| Explicit types | No `var`; the declared type is written out | `csharp-coding-standards`, `code-reviewer` (project rule; the forked `dotnet-patterns` skill uses `var` and is overridden) |
| Immutability | Records for requests, responses and read models; `init`-only properties; `sealed` by default | `csharp-coding-standards`, `dotnet-patterns` |
| Exceptions | Specific domain exceptions (`DuplicateCodeException`, `InsufficientStockException`, `UnknownCodeException`); caught narrowly and only at the boundary handler | `csharp-coding-standards` |
| Dependency injection | Constructor injection only; no `new` of a collaborator inside a class; lifetimes chosen deliberately and scope validation on | Assignment B4; `dotnet-patterns` |
| Interfaces | An interface exists only where a unit test needs a seam or a second implementation exists. Expected seams: the transfer store, the current-user accessor | YAGNI; `csharp-coding-standards` |
| Async | Async for I/O only; cancellation token flows from the request to the Dapper command; no blocking on tasks; no `async void` | `dotnet-patterns` anti-patterns; B4 |
| Size | Methods under ~20 lines, classes under ~200; split by responsibility | `csharp-coding-standards` |
| Formatting | `.editorconfig` committed; `dotnet format` before every commit; warnings as errors; file-scoped namespaces; one public type per file | `csharp-coding-standards` |
| Layout | Vertical slices in one API project (see the implementation plan). This is a documented deviation from the standards' four-project template, justified by the five-endpoint size and SCAD's stated house pattern ("one endpoint, one Dapper query, one integration test, one UI page"). Split trigger: a second consumer of application logic, such as an outbox relay worker | Decision, 2026-09-24 |
| SOLID | Single responsibility per slice component; open for extension through the store seam; substitutable fakes in tests; small interfaces; dependency on abstractions only where an abstraction exists | `csharp-coding-standards`; low-level design notes |

## Glossary (prevents naming drift, G32)

| Term | Meaning | Where the word appears |
|---|---|---|
| Product | Catalogue item identified by `code` | Table `products`, route `/products`, type `Product` |
| Warehouse | Location identified by `code` | Table `warehouses`, route `/warehouses`, type `Warehouse` |
| Stock | Quantity of one product in one warehouse | Table `stock`, route `/stock`, type `StockLevel` |
| Transfer order | A movement of quantity from a source warehouse to a destination warehouse | Table `transfer_orders`, route `/orders`, type `TransferOrder` |
| User | An authenticated principal | Table `users`, route `/auth/login` |
| Link | A user's membership of a warehouse (the authorization scope) | Table `user_warehouses` |
| Scope | The set of warehouses linked to the current user | The predicate every scoped query carries |

## Development workflow gates

| Gate | When | What must be true |
|---|---|---|
| Task start | Before any code for a task | Its row in the task list names the proving test and the commit message |
| Task end | Before the commit | Proving test green; `dotnet format` clean; explain-back checkpoint done (can you say what every line does?) |
| Commit | Every commit | Subject matches `^(feat\|fix\|test\|docs\|chore\|refactor\|build\|ci)\((repo\|api\|db\|products\|warehouses\|stock\|orders\|auth\|ui\|e2e\|compose\|readme\|tests)\): [a-z].{8,}$` and is at most 72 characters — the scope is mandatory and must come from the fixed list, so the hook enforces both the format and the vocabulary; pre-commit hook ran build + unit tests + constraint scan |
| Merge | If a branch/PR flow is used | Rebase or fast-forward only. **A squash merge would produce exactly the "one commit containing everything" the assignment forbids** |
| Cut line | 3:20 elapsed | If authentication is not green, drop the e2e spec, then Compose, in that order; record the cuts in the README |
| Pre-submit | Before sharing the repo | History audit (every subject matches the regex, no merge commits); fresh-clone smoke test; README checklist complete; AI section complete |

## Precedence of authorities

When two sources disagree, the higher one wins:

1. A starter repository's README, if one is sent (A1)
2. The assignment's hard constraints and scored requirements
3. A starter repository's existing code pattern — follow it even when you disagree; put the objection in the README (A4)
4. This design set
5. Personal coding standards

## Governance

- This constitution supersedes every other practice note for this project.
- An amendment is a dated line in the table below, with the reason. Silent drift is a violation.
- Every task in the task list cites the principle(s) it serves; the reviewer checks compliance before the commit.
- Complexity beyond this document (a new project, a new package, a new abstraction) requires a row in the implementation plan's complexity-tracking table naming the simpler alternative rejected and why.

| Version | Date | Change |
|---|---|---|
| 1.0.0 | 2026-09-24 | Ratified from the simplified spec, the author's rulings and the standards sources |
| 1.1.0 | 2026-09-24 | Principle III: SQL may also live in the database plumbing in `Shared/Persistence/`, and scoped queries write the link join in full instead of sharing a fragment. Reason: a shared fragment breaks "never concatenated", and the schema lock and health check need SQL (ADR-008) |
