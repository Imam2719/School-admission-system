# Assumptions & decisions

- **IDs:** `c_<12 hex>` for classes and `h_<12 hex>` for holds (matching the examples in the spec).
- **Student ID:** any non-empty string up to 64 characters; there is no login, so the header is trusted.
- **Error codes:** 400 missing header/invalid body, 403 hold belongs to another student, 404 not found, 409 no seats / duplicate application / hold expired or not confirmable. Errors are `{ "error": "message" }`.
- **One active application per class:** `HELD` (not expired) or `CONFIRMED` counts as active. After cancel or expiry the student may apply again.
- **Cancel rules:** cancelling a `CONFIRMED` application is rejected (409, the fee was "paid"). Cancelling twice returns 204 (idempotent).
- **Confirm is idempotent:** confirming an already confirmed hold returns 200 and does not change counts. Confirming an expired hold returns 409.
- **Expiry:** a `HELD` hold with `expiresAt <= now` is treated as expired in every count and in `GET` responses. A background job (every 5 s) and hold creation additionally write `EXPIRED` to the DB.
- **`GET /holds/{id}` and `GET /classes`** do not need the student header (not required by the spec).
- **Payment:** simulated; "Pay admission fee" simply confirms the hold.
- **Countdown:** based on the server `expiresAt` and the browser clock; a large clock difference between browser and server would shift the displayed timer (the server is always the source of truth).
- **Schema:** created with `EnsureCreated` on start-up (with retry) instead of migrations, to keep `docker compose up` to a single step.

## Bonus: protection across multiple backend processes

Overselling is prevented **inside PostgreSQL**, not in application memory:

1. Every create-hold / confirm / cancel runs in a transaction that first executes `SELECT ... FROM classes WHERE "Id" = @id FOR UPDATE`. Only one transaction at a time can hold that row lock, so all seat changes for one class are serialized, no matter which backend process (port 8080, 8081, ...) they come from. After acquiring the lock, the transaction counts seats with a fresh read (READ COMMITTED), so it sees everything the previous lock holder committed.
2. A **partial unique index** `(ClassId, StudentId) WHERE Status IN ('HELD','CONFIRMED')` enforces "one active application per class" at DB level, even if application code had a bug.
3. A `CHECK (TotalSeats > 0)` constraint on classes.

No in-process locks (`lock`, `SemaphoreSlim`) are used, since those would only work for a single process.

## Extension: guardian application and admin approval
- Flow: guardian fills the application form (guardian name, student name, NID, class) -> the system generates a 14-digit Student ID (`<year><10-digit sequence>`) -> an admin approves -> approval creates the seat **hold** (the timer starts then) -> the guardian confirms the fee with the normal `POST /holds/{id}/confirm`. The 8 contract endpoints are unchanged.
- Extra endpoints: `POST /applications`, `GET /applications?nid=`, `GET /admin/applications`, `POST /applications/{id}/approve`, `POST /applications/{id}/reject`.
- NID must be 10, 13 or 17 digits. The same child (NID + name) keeps one Student ID. No login, so admin and guardian pages are separated in the UI only, not by security.
- A pending application holds no seat. Approving takes the same class row lock as every other seat change, so it can never oversell. If the guardian misses the payment window, the admin can approve again.
- Existing databases need a reset (`docker compose down -v`) because the schema gained a table and a sequence.
