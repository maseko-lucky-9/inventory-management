# SCAD Skills Test — Feature Specification

> Copied into this repo from my pre-clock design notes, with Obsidian formatting removed. Rule IDs such as H4, C1 or G35 come from the simplified spec. That note and the others named here (system design, implementation plan, task list and so on) are not in this repo; the decisions they led to are in [docs/decisions/](decisions/).

> **What this note is**
> The spec-kit `specify` artifact: prioritised user stories with Given/When/Then acceptance scenarios, numbered functional requirements traced to the simplified spec, the key entities, the edge cases, measurable success criteria and the assumptions taken. It says **what** and **why**; the system design says **how**.

> **Why / what to learn — independently testable stories**
> Spec-kit orders stories so that implementing only P1 still yields a viable product. That is exactly what SCAD's drop order does: P1 (the transfer) alone earns 45 of the 100 points. Writing stories this way makes the four-hour cut line a feature of the spec, not an emergency.

**Input:** the assignment as simplified in the simplified spec. **Tier:** Senior (superset). **Status:** Draft until the tier and the starter-repo question are confirmed by the recruiter.

## User scenarios & testing

### User story 1 — Transfer stock between warehouses (Priority: P1)

An operator moves a quantity of one product from a warehouse they are linked to into another warehouse. The move either happens completely or not at all, and can never take a warehouse below zero — even when two operators act on the same stock at the same instant.

**Why this priority:** it is the scored core (S1 25 + S2 20) and drop-order item D1. Everything else is scaffolding around it.

**Independent test:** with two warehouses and stock seeded, call `POST /orders` and read both levels back with `GET /stock`.

**Acceptance scenarios:**

1. **Given** product P has 10 units in warehouse A and 0 in B, **When** a linked user transfers 7 from A to B, **Then** the response is 201, A shows 3, B shows 7, and one transfer-order record exists.
2. **Given** P has 3 units in A, **When** a user transfers 7 from A to B, **Then** the response is 400 with code `insufficient_stock` and a message naming the product, the source warehouse, the requested 7 and the available 3; A still shows 3 and B is unchanged.
3. **Given** any state, **When** source and destination codes are equal, **Then** the response is 400 with code `validation_failed` naming the self-transfer rule, and nothing changes.
4. **Given** any state, **When** the product code or either warehouse code does not exist (or the source is not linked to the caller), **Then** the response is 400 naming the unknown code, and nothing changes.
5. **Given** P has 10 in A, **When** two transfers of 7 from A arrive at the same instant, **Then** exactly one succeeds, the other is a 400 `insufficient_stock`, and A shows 3.
6. **Given** P has 10 in A, **When** ten transfers of 3 arrive in parallel, **Then** exactly three succeed and seven fail; A shows 1, the destination shows 9, and three transfer-order records exist.
7. **Given** P has stock in A and in B, **When** a transfer A→B and a transfer B→A arrive at the same instant, **Then** both complete with no 500 and no deadlock error surfaces to the caller.
8. **Given** B has no stock row for P, **When** a transfer A→B succeeds, **Then** a stock row for P in B is created with the transferred quantity.
9. **Given** the transfer is applied to A but the destination step fails, **When** the transaction ends, **Then** A is unchanged (atomicity).

### User story 2 — Register products and warehouses (Priority: P2)

A user creates products (code + description) and warehouses (code + name). Codes are unique; a duplicate is refused with a message that names the clash.

**Why this priority:** every other story needs the master data; the 409 path is a named scoring item.

**Independent test:** `POST /products` twice with the same code; expect 201 then 409.

**Acceptance scenarios:**

1. **Given** no product `SKU-1`, **When** it is posted with a description, **Then** 201 with the product body and a Location header, and `GET /products` includes it.
2. **Given** `SKU-1` exists, **When** it is posted again, **Then** 409 with code `duplicate_product_code` and a message containing `SKU-1`.
3. **Given** a request missing `code` or `description`, **When** posted, **Then** 400 `validation_failed` with an `errors` map keyed by field name.
4. **Given** no warehouse `WH-1`, **When** posted with a name by user U, **Then** 201, and U is now linked to `WH-1` so `GET /warehouses` for U includes it.
5. **Given** two users post the same warehouse code at the same instant, **Then** exactly one gets 201 and the other 409.

### User story 3 — Receive and query stock (Priority: P3)

A user records initial stock for a product in a linked warehouse and reads stock levels by product (across their linked warehouses) or by warehouse.

**Independent test:** `POST /stock` then `GET /stock?productCode=` and `GET /stock?warehouseCode=`.

**Acceptance scenarios:**

1. **Given** product P and linked warehouse A, **When** 5 units are posted, **Then** 200 with the resulting level 5; posting 5 again returns 200 with 10 (additive).
2. **Given** P has stock in A and B and the user is linked to both, **When** `GET /stock?productCode=P`, **Then** both rows are returned with product code, warehouse code and quantity.
3. **Given** the user is linked only to A, **When** `GET /stock?productCode=P`, **Then** only the A row is returned.
4. **Given** the user is linked only to A, **When** `GET /stock?warehouseCode=B`, **Then** 404 — indistinguishable from an unknown code.
5. **Given** no query parameter, **When** `GET /stock`, **Then** 400 `validation_failed`.
6. **Given** a quantity of 0 or negative, **When** posted, **Then** 400.

### User story 4 — Warehouse-scoped access (Priority: P4, Senior)

A user must authenticate, and may then see and act only on the warehouses linked to them. Enforcement is in the data layer.

**Independent test:** log in as a user of A; assert B's stock is invisible and B is absent from `GET /warehouses`; call without a token and get 401.

**Acceptance scenarios:**

1. **Given** valid demo credentials, **When** `POST /auth/login`, **Then** 200 with a bearer token; **When** invalid, **Then** 401 `invalid_credentials`.
2. **Given** no or an invalid token, **When** any non-login endpoint is called, **Then** 401.
3. **Given** user U linked to A only, **When** `GET /warehouses`, **Then** only A is listed.
4. **Given** U linked to A only, **When** `POST /stock` targets B, **Then** 400 `unknown_warehouse_code`.
5. **Given** U linked to A only, **When** a transfer from B is requested, **Then** 400 `unknown_warehouse_code`; **When** a transfer from A into B is requested, **Then** it proceeds (destination may be any existing warehouse).
6. **Given** a user with no links, **When** `GET /warehouses` or `GET /stock?...`, **Then** an empty list / 404 respectively; product creation still works.

### User story 5 — Browse and create products in the UI (Priority: P5)

A person opens the UI, logs in, sees the product list, opens a product to see its details and scoped stock levels, and creates a product from a form that shows server-side errors in place — including the duplicate-code 409. Loading, empty and error states are visible.

**Independent test:** one Playwright spec: login → list → create → duplicate → see the error on the field → open detail.

**Acceptance scenarios:**

1. **Given** the API is slow, **When** the list page opens, **Then** a loading state is visible until data arrives.
2. **Given** no products, **When** the list page opens, **Then** an explicit empty state is shown.
3. **Given** the API is down, **When** any page loads, **Then** an error state with a retry action is shown.
4. **Given** the create form, **When** submitted with a duplicate code, **Then** the `code` field shows the server's 409 message and the form stays filled.
5. **Given** a product, **When** its detail page opens, **Then** code, description and the stock rows for the user's linked warehouses are shown.

### User story 6 — Run everything with one command (Priority: P6, Senior)

A grader with Docker runs `docker compose up` and gets the API and a database with the schema applied and demo users seeded.

**Acceptance scenarios:**

1. **Given** a fresh clone, **When** `docker compose up`, **Then** within two minutes the API answers `/health` with the database reachable.
2. **Given** only the assignment's `docker run` one-liner and `dotnet run`, **When** the API starts, **Then** it applies the schema itself and serves requests.

### User story 7 — Order history (Priority: P7, optional)

A user lists past transfer orders that touch their linked warehouses.

**Acceptance scenario:** **Given** transfers exist, **When** `GET /orders`, **Then** those with a linked source or destination are returned, newest first.

## Edge cases

- Quantity is zero, negative, non-integer, or larger than a 32-bit integer.
- Destination increment would overflow → 400 `quantity_out_of_range`, transaction rolled back.
- Source has no stock row (treated as 0).
- Codes with surrounding whitespace (trimmed), over 50 characters, or with characters outside letters, digits, `.`, `_`, `-`.
- Malformed JSON or an empty body → 400 `malformed_request`; a body with a content type other than JSON → 415 `unsupported_media_type`. Both use the standard envelope.
- Unknown JSON fields → ignored.
- Both `productCode` and `warehouseCode` on `GET /stock` → intersection.
- Two users create the same warehouse code at the same instant → one 409.
- A user linked to exactly one warehouse (the assignment's literal case) can still transfer out to any other warehouse.
- Token expired → 401; the UI returns to the login page and keeps unsaved form input.
- Database unreachable → `/health` reports unhealthy; requests return 503 with the envelope.
- Deadlock detected by the database (should not occur with lock ordering) → 503 `concurrency_conflict` with Retry-After, never a 500.

## Requirements

### Functional requirements

| ID | Requirement | Trace |
|---|---|---|
| FR-001 | The system MUST expose `GET /products`, `POST /products`, `GET /products/{code}` | E1, E2, G2 |
| FR-002 | The system MUST reject a duplicate product or warehouse code with 409 and a message naming the code | E3, E5 |
| FR-003 | The system MUST expose `GET /warehouses` and `POST /warehouses`, and MUST link the creator to the new warehouse in the same transaction | E4, E5, G37 |
| FR-004 | The system MUST expose `POST /stock` (additive), `GET /stock?productCode=`, `GET /stock?warehouseCode=` | E6, E7, E8, G5, G6 |
| FR-005 | The system MUST expose `POST /orders` and MUST move quantity from source to destination atomically | E9, E11 |
| FR-006 | The system MUST refuse a transfer that would take the source below zero with 400 and a message naming product, source, requested and available quantities | E10, C1 |
| FR-007 | The system MUST never oversell under concurrent transfers, without relying on an application-level read-check-write | C1, C2 |
| FR-008 | The system MUST reject self-transfers and unknown codes with 400 and a message naming the rule or code | E12, E13 |
| FR-009 | The system MUST record one transfer-order row per successful transfer in the same transaction | E14 |
| FR-010 | The system MUST validate every request body and query with field-level errors | B3 |
| FR-011 | The system MUST return every error as an RFC 9457 Problem Details document with `code` and `traceId` | B2 |
| FR-012 | The system MUST require a bearer token on every endpoint except `POST /auth/login` and `GET /health` | T9 |
| FR-013 | The system MUST restrict warehouse lists, stock reads, stock receipts and transfer sources to warehouses linked to the caller, enforced inside the data-access queries | T13, G35–G43 |
| FR-014 | The system MUST respond identically for an unknown warehouse code and a warehouse the caller is not linked to | G39 |
| FR-015 | The system MUST create its own schema from `db/schema.sql` and seed demo users from `db/seed.sql`, idempotently, at startup | H4, G3 |
| FR-016 | The system MUST expose `/health` reporting database reachability | G29 |
| FR-017 | The UI MUST list products, show a product's detail with scoped stock, create a product surfacing server-side errors per field, and show loading, empty and error states | U1–U4 |
| FR-018 | The repository MUST include a Docker Compose file starting PostgreSQL and the API with health-based ordering | T11, G23 |
| FR-019 | The repository MUST include one Playwright end-to-end spec covering login → list → create → duplicate error → detail | T12 |
| FR-020 | The system MUST log structured request logs and include the trace id in every error response | G29 |
| FR-021 | The system MUST rate-limit `POST /auth/login` | G30 |
| FR-022 | The repository MUST contain unit tests for validators, the transfer service, and the error translator; integration tests through HTTP against a real PostgreSQL including the parallel, opposing-direction and lock-interleaving concurrency tests, and authorization tests | B5, B6, G19, G21 |
| FR-023 | The README MUST contain setup, run, test, example calls, the concurrency paragraph, the authorization-in-data-layer explanation, what was left out, the AI-usage section, the test catalogue, and start/end times | H7, T13, H8, G18, G27 |

### Rule coverage beyond the FRs

Every rule ID in the simplified spec that is a constraint, tier obligation, drop-order item, scoring line, interview prompt or appendix rule — rather than a behaviour — is covered here, so no ID is orphaned.

| Rule IDs | Covered by |
|---|---|
| H1 (.NET + UI), H2 (PostgreSQL), H3 (no ORM), H4 (DDL shipped) | [constitution](constitution.md) hard-constraint table + gates; FR-015, FR-018 |
| H5 (four hours), H10 (small, correct, tested) | the implementation plan budget and cut line; constitution II |
| H6 (commits), H7 (README), H8 (AI), H9 (submission) | Constitution V and VIII; the task list T01, T21, T22; FR-023 |
| B1 (API framework) | Minimal APIs — the system design §4 |
| B2, B3, B4, B5, B6 | FR-010, FR-011, FR-022 |
| C1, C2 | FR-006, FR-007 |
| C3 (pick a strategy), C4 (README paragraph) | the concurrency design; the task list T11, T21 |
| U1, U2, U3, U4 | FR-017 |
| U5 (any framework, Vue preferred) | Vue 3 + Vite — the task list T13 |
| T1, T2, T3, T4, T5, T6, T7, T8 (all-tier and INT obligations) | FR-001–FR-011, FR-017, FR-022 |
| T9, T10, T11, T12, T13 (Senior obligations) | FR-012, FR-013, FR-014, FR-018, FR-019 |
| D1, D2, D3, D4, D5, D6, D7, D8, D9, D10 (drop order) | the task list ordering; the simplified spec D table |
| S1, S2, S3, S4, S5, S6, S7 (scoring) | the overview scoring table; the rubric traceability |
| I1, I2, I3, I4, I5 (interview prompts) | the interview-defence notes |
| A1, A2, A3, A4 (starter repo) | the starter-repo gap analysis; constitution precedence rule |

### Key entities

| Entity | Represents | Key attributes | Relationships |
|---|---|---|---|
| Product | A catalogue item | code (unique), description, created timestamp | has many stock levels |
| Warehouse | A physical location | code (unique), name, created timestamp | has many stock levels; has many linked users |
| StockLevel | Quantity of a product in a warehouse | quantity (never negative), updated timestamp | identified by (product, warehouse) |
| TransferOrder | One completed movement | quantity (positive), created timestamp, created-by user | references product, source warehouse, destination warehouse (source ≠ destination) |
| User | An authenticated principal | username (unique), password hash | linked to zero or more warehouses |
| UserWarehouseLink | Authorization scope | — | (user, warehouse) pair, unique |

## Success criteria

| ID | Measurable outcome |
|---|---|
| SC-001 | Ten parallel transfers of 3 against 10 units yield exactly 3 successes and 7 refusals, and the source ends at 1 — in every run of the integration suite |
| SC-002 | The deterministic lock-interleaving test proves the blocked transfer re-evaluates against the committed quantity |
| SC-003 | 100% of non-2xx responses in the integration suite carry the Problem Details envelope with `code` and `traceId` |
| SC-004 | A fresh clone reaches a healthy API via `docker compose up` in under two minutes on the grader's machine |
| SC-005 | The Playwright spec passes against the composed stack |
| SC-006 | `git log` contains only Conventional-Commit subjects, one task per commit, no merge or squash commits |
| SC-007 | Every README section demanded by H7 exists and the fresh-clone smoke test follows it verbatim without improvisation |
| SC-008 | The candidate can explain every line in the repo without notes (explain-back checkpoints all passed) |

## Assumptions

- The recruiter's tier is Senior or lower; designing for Senior and trimming is safe.
- No starter repository unless one arrives; if it does, the starter-repo gap analysis runs first and its conventions take precedence.
- Codes are case-sensitive after trimming `[ASSUMED]`.
- `POST /stock` is additive `[ASSUMED]`.
- Products are a global catalogue; only warehouses, stock and orders are scoped (author's ruling).
- A transfer destination may be any existing warehouse (author's ruling).
- Warehouse links come from seed data and creator auto-linking; link administration is out of the four hours (author's ruling).
- Warming SDK, package, image and Node caches before the clock starts is acceptable; the README states it.
- The grader has Docker and the .NET 10 SDK available; `global.json` pins the SDK.
