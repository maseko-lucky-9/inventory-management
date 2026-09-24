# ADR-007: Vue and Vite UI with a dev proxy

- Status: Accepted
- Date: 2026-09-24
- Deciders: Thulani Maseko
- Traces: H1, U1, U2, U3, U4, U5, T7, T8, T12, S6, D6, D7, G13, G14; FR-017, FR-019; question 6 to SCAD

## Context

- H1: a small UI in front of the API.
- U5: any framework. Vue 3 / Nuxt 4 is preferred (SCAD's stack); React/Next, Angular and Svelte are acceptable and cost no marks. CSS is not scored; state handling and the write path are.
- U1–U4: list products, view one, create one with server-side validation errors shown in the form (including the duplicate-code 409), and visible loading, empty and error states.
- G14: the Vite dev server is a different origin from the API.
- The assignment describes Compose as API + database. Question 6 to SCAD asks whether to include the UI; the default is no.
- T12: one Playwright end-to-end spec. D6 and D7 give the UI 30 minutes in total.

## Decision

- A Vue 3 + Vite + TypeScript SPA in `ui/`, with Vue Router, a small fetch wrapper (`ui/src/api/client`) that parses Problem Details, and no UI component library.
- Pages: login, `ProductList`, `ProductDetail` (the product plus its stock rows from `GET /stock?productCode=`, so scoping is visible in the UI), and a create form.
- Each page has an explicit state: loading, then empty, data, or error with a retry action.
- The create form disables submit while a request is in flight, shows the 400 `errors` map under each field, shows the 409 `duplicate_product_code` under `code`, and goes to the detail page on success.
- The token is kept in memory and sent as a bearer `Authorization` header on every call. A 401 returns to the login page, keeping the intended route.
- The Vite dev proxy forwards API calls so the SPA and the API share one origin. The API has no CORS policy.
- One Playwright spec in `e2e/` (base URL = the Vite dev server) runs against the composed stack.

## Constraints this decision must honour

- The UI reflects permissions; it never enforces them (T13, constitution VI; see ADR-006).
- Server-side validation is the source of truth. `errors` keys use the JSON property names, so the form can bind them to fields (G13).
- Compose stays API + database unless SCAD answers question 6 otherwise.

## Consequences

- Positive: SCAD's preferred framework family without the setup cost of server-side rendering.
- Positive: no CORS surface to misconfigure.
- Cost accepted: not Nuxt specifically.
- Cost accepted: the UI depends on the Vite dev server, so the e2e spec needs it running next to Compose.
- Cost accepted: a page reload loses the in-memory token.

## Alternatives considered

- **Nuxt 4.** SCAD's other preferred option. Rejected for its SSR setup cost inside a 30-minute UI budget.
- **React / Next.js.** Acceptable to SCAD and costs no marks, but not their stack.
- **A CORS policy in the API.** Adds a cross-origin surface to configure and get wrong. The dev proxy avoids it.
- **A UI component library.** Not needed: CSS is not scored (U5).
- **The UI as a Compose service.** Open as question 6. The default follows the assignment's wording: API + database.

## How we prove it

- T13 manual checks: API stopped (error state), empty database (empty state), throttled network (loading state). Screenshots with `playwright-cli` in light and dark themes.
- T14 manual check of the duplicate path: the 409 message appears under `code`, and the form stays filled.
- T18 manual check: login works, the token goes with every call, and a 401 returns to login.
- T20 Playwright spec: "alice logs in, sees the list, creates a product, is shown the 409 on the code field when repeating it, and opens the product's detail". Target: it passes against the composed stack.

## Revisit when

- SCAD asks for the UI in Compose (question 6): add a UI service, and decide between a reverse proxy and a CORS policy.
- A real deployment: a reverse proxy terminates TLS in front of the API, and the Vite dev proxy no longer applies.
- A starter repository arrives with its own UI page pattern (A2, A4): follow it.
