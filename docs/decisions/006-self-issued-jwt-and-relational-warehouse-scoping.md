# ADR-006: Self-issued JWT and relational warehouse scoping

- Status: Accepted
- Date: 2026-09-24
- Deciders: Thulani Maseko
- Traces: T9, T10, T13, H9, I3, R6, G12, G21, G22, G30, G35, G36, G37, G38, G39, G40, G41, G42, G43, G44; FR-012, FR-013, FR-014, FR-021; constitution VI

## Context

- T9: authentication in the API and the UI.
- T13 (Senior): a user is linked to a warehouse and may only see stock for that warehouse. Enforce it in the data layer, not in the UI, and say in the README why the UI is the wrong place.
- H9: the grader must run it from the README without asking. G22: no secrets in git.
- G36: the assignment's literal case is a user linked to one warehouse. If both ends of a transfer had to be linked, that user could never transfer.
- G44: authentication and scoping get a 30-minute slot.

## Decision

Authentication:

- `POST /auth/login` checks the username and password against `users` with the framework's PBKDF2 password hasher. It returns a self-issued, HMAC-signed JWT: subject = user id, issuer, audience, 60-minute lifetime, no refresh token.
- The framework JwtBearer handler validates signature, issuer, audience and lifetime. A fallback policy requires an authenticated user on every endpoint except `POST /auth/login` and `GET /health`.
- Login is rate-limited with a fixed window per client IP (G30). The signing key comes from the environment (`Jwt__SigningKey` in the local, gitignored `.env`; `.env.example` commits only a placeholder). In Development and Test, if it is unset, a random per-process key is used with a logged warning. The demo users' password comes from `DemoUsers__Password` the same way and is hashed at startup (ADR-005).

Authorization:

- Links live in `user_warehouses`. `ICurrentUser` exposes only `UserId`. One shared link-join fragment (`ScopeSql`) is applied inside every scoped query in `WarehouseStore`, `StockStore` and `TransferStore`. Links are read per query.
- Products are a global catalogue (G35). A transfer's source must be linked; its destination may be any existing warehouse (G36). Creating a warehouse links its creator in the same transaction (G37). A user with no links sees empty lists (G40). `GET /orders`, if built, returns transfers whose source or destination is linked (G43).
- Unknown and unlinked warehouses respond identically: 400 `unknown_warehouse_code` in bodies, 404 in paths and queries (G39). `created_by` is recorded on each transfer.

## Constraints this decision must honour

- Enforced in the data layer, never the UI (T13, constitution VI).
- SQL stays in Store files with named parameters (H3). Secrets come from configuration, never source; passwords and tokens are never logged (G22).
- Trimming to Intermediate drops the join and the link table; nothing else changes (G42).

## Consequences

- Positive: the rule is visible in the SQL the assessor reads. A new endpoint that reuses a store inherits it. Link changes apply immediately.
- Cost accepted: the API handles passwords, mitigated by the framework hasher and the login rate limit.
- Cost accepted: no revocation before the 60-minute expiry. Scope links are read per request, so only identity is stale.
- Cost accepted: one join per scoped query; the signing key sits in configuration; the SPA keeps the token in memory, so a reload means logging in again.

## Alternatives considered

- **OAuth2 / OIDC with an external authorization server (authorization code + PKCE).** Not an either/or: OAuth2 is a delegation protocol, JWT is a token format. Nothing here is delegated; there is one first-party UI and one API. The OAuth2 grant that matches "password in, token out" is the password grant, which RFC 9700 §2.4 says must not be used. An external IdP would stop the repo running from the README alone and does not fit the 30-minute slot. **Production migration path:** an OIDC provider with authorization code + PKCE through a BFF. The API becomes a resource server, JwtBearer points at the provider's `Authority`, and the scope join does not change.
- **Cookie session.** JWT bearer is simpler for curl, tests and the demo.
- **Token-claim scoping (warehouse ids embedded in the token).** Link changes would not apply until the token expires. Rejected in favour of reading links per query.
- **Row-level security.** Deferred defence in depth: the next layer, which even a forgotten predicate cannot skip. The multi-tenancy design (I3) describes it: a policy, FORCE enabled, an app role that is not the owner, and a transaction-local setting so pooled connections cannot leak it.
- **Enforcement in the UI.** The browser is under the user's control and curl skips it. A UI check hides data; it does not protect it.
- **Endpoint-level checks.** Better than the UI, but forgettable per route.
- **Roles, refresh tokens, a link-admin endpoint.** Scope creep for four hours (R6). The link endpoint is designed and deferred.

## How we prove it

- Authentication (T15): "valid demo credentials return a bearer token"; "a wrong password returns 401 invalid_credentials"; "every route except login and health returns 401 without a token".
- Scoping (T16, G21): "alice cannot see WH-B in the warehouse list or in stock queries"; "querying stock of WH-B as alice returns 404, same as an unknown warehouse"; "alice cannot transfer out of WH-B (400 unknown_warehouse_code)"; "alice can transfer from WH-A into WH-B"; "a warehouse created by carol is immediately visible to carol"; "carol with no links sees an empty warehouse list".
- Scope guard (T17, source scan): "every query in WarehouseStore, StockStore and TransferStore that reads warehouses or stock references the scope fragment". Mutation: remove the fragment from one query; the test must go red; restore it.

## Revisit when

- The system goes to production: move to an OIDC provider, and the API becomes a resource server.
- The API becomes multi-tenant (I3): tenant id from the validated token only, a mandatory predicate, tenant-scoped unique keys and composite foreign keys, then row-level security.
- Tokens must be revocable, or users need link administration.
