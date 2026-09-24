# ADR-004: Domain exceptions, Problem Details and FluentValidation

- Status: Accepted. Amended 2026-09-24: a body with a content type other than JSON returns 415 `unsupported_media_type` in the same envelope, because the framework rejects it before binding
- Date: 2026-09-24
- Deciders: Thulani Maseko
- Traces: B2, B3, B5, E3, E10, E12, E13, U3, G8, G11, G13, G15, G29, G39; FR-010, FR-011, FR-020; constitution VII

## Context

- B2: global exception handling, a consistent JSON error shape, and correct status codes.
- B3: validation via data annotations or FluentValidation.
- E3 and E10: a duplicate code is a 409 with a useful message; insufficient stock is a 400 that says what was wrong, not just that something was.
- U3 and G13: the UI must show server-side validation errors on the form field they concern, including the duplicate-code 409.
- G15: .NET 10 ships built-in Minimal API validation.
- The UI and the tests need a stable, machine-readable error identity, not only a status code.

## Decision

- Every non-2xx response is an RFC 9457 Problem Details document with `type`, `title`, `status`, `detail`, `instance`, a stable `code` and a `traceId`, plus `errors` for validation. Success bodies are bare resources or arrays.
- Expected failures are domain exceptions: `DuplicateCodeException`, `UnknownCodeException`, `NotFoundException`, `InsufficientStockException`, `QuantityOutOfRangeException`, `ConcurrencyConflictException`, `DatabaseUnavailableException`. One `DomainExceptionHandler`, outermost in the pipeline, maps each type to a status and `code`, adds the trace id, and logs 5xx at error level. No stack traces leave the API.
- `DbErrorTranslator` is the one place database error codes become domain exceptions: unique violation to duplicate; numeric overflow to out of range; deadlock, serialization failure or lock not available to conflict; check violation to `null`, which the handler treats as a defect (500).
- No "does this code exist?" SELECT before an INSERT. The unique constraint decides; a pre-check would race.
- FluentValidation validators run in a `ValidationFilter<TRequest>` endpoint filter. Errors are keyed by the camelCase JSON property names so the UI can bind them to fields.
- An unknown or unlinked code in a request body is 400 (`unknown_product_code` / `unknown_warehouse_code`). In a path or query it is 404 (`product_not_found` / `warehouse_not_found`). RFC 9110: 404 means the target resource was not found, and `/orders` exists.

| Status | `code` |
|---|---|
| 400 | `malformed_request`, `validation_failed`, `unknown_product_code`, `unknown_warehouse_code`, `insufficient_stock`, `quantity_out_of_range` |
| 401 | `unauthorized`, `invalid_credentials` |
| 404 | `product_not_found`, `warehouse_not_found` |
| 409 | `duplicate_product_code`, `duplicate_warehouse_code` |
| 429 | `too_many_requests` |
| 503 | `concurrency_conflict` (with Retry-After), `database_unavailable` |
| 500 | `internal_error` |

## Constraints this decision must honour

- One error contract (constitution VII): every error path returns the same envelope.
- Exceptions are specific, and are caught narrowly and only at the boundary handler.
- Unknown and out-of-scope warehouses respond identically (G39), so an error never confirms a warehouse outside the caller's scope.
- Passwords and tokens are never logged (G29).

## Consequences

- Positive: one path for every failure. A new refusal reason is a new exception plus one row in the handler's table; endpoints do not change.
- Positive: the translator is a pure function, so its mapping is unit-tested without a database.
- Cost accepted: exceptions for expected outcomes, negligible at this load.
- Cost accepted: one package, and a filter the framework now provides natively.
- Cost accepted: some reviewers expect 404 for an unknown code in a body.

## Alternatives considered

- **Result pattern.** Rejected. Exceptions are the native mechanism, give one path to a global handler, and match the assignment's "global exception handling". A Result type would add a second error flow to keep consistent with the first.
- **.NET 10 built-in Minimal API validation (DataAnnotations + IValidatableObject).** A fair alternative that would remove a package. FluentValidation was chosen for expressive rules, trivially unit-testable validators and familiarity (my choice).
- **A `{ success, data, error }` envelope.** The HTTP status already carries success. It is a Next.js pattern from another standards skill.
- **404 or 422 for unknown codes in a body.** The problem is a reference inside the body, not the target resource. 400 also matches the assignment's own choice for insufficient stock.

## How we prove it

- `DbErrorTranslatorTests` (unit): unique violation to duplicate, overflow to out of range, deadlock to conflict, check violation to defect. The duplicate case is the "duplicate code" unit test B5 asks for.
- Validator unit tests for every rule: code pattern and length, required fields, positive quantity, self-transfer, at least one stock filter.
- Integration: "posting a product twice returns 201 then 409 whose detail names the code"; "malformed JSON returns 400 malformed_request with the envelope"; "every error response carries code and traceId". Target: 100% of non-2xx responses in the suite carry the envelope.
- The mutation check "delete the guard predicate" in ADR-003 depends on a check violation being a 500, not a 400.

## Revisit when

- A starter repository arrives with its own error or validation pattern (A4): follow it.
- Removing a package matters more than rule expressiveness: the built-in validation is the recorded fallback.
