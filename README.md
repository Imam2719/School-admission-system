# School Admission System

A first-come-first-served school admission system. Admins create classes with a limited number of seats. Guardians apply, a seat is **held** temporarily, and the guardian must **confirm** (pay the admission fee) before the hold expires. If they don't, the seat is released automatically.

Thousands of guardians can apply at the same time, and the system never sells more seats than a class has.

**Stack:** .NET 8 (minimal API, EF Core) · PostgreSQL 16 · React 18 (Vite) · Docker

---

## Screenshots

> Add your screenshots to `docs/screenshots/` and keep the file names below, or change the links.

| Home (choose role) | Admin: applications |
|---|---|
<img width="1273" height="717" alt="image" src="https://github.com/user-attachments/assets/f986cbeb-5784-4cf9-9b73-1e8e345166c6" />

<img width="1920" height="1251" alt="image" src="https://github.com/user-attachments/assets/ab56f421-fafc-4ef0-8f53-492973e0134e" />


| Admin: classes and seats | Guardian: new application |
|---|---|
<img width="1920" height="2122" alt="image" src="https://github.com/user-attachments/assets/0aad8b3c-38c4-4491-890f-b61db0eea936" />

<img width="1273" height="712" alt="image" src="https://github.com/user-attachments/assets/0c6bf2be-f5ab-446e-9b42-137baba5cc7c" />



| Guardian: track and pay (countdown) | Class is full |
|---|---|
<img width="1279" height="716" alt="image" src="https://github.com/user-attachments/assets/da0a6029-248d-44a0-8266-d4fe05194486" />

<img width="1269" height="716" alt="image" src="https://github.com/user-attachments/assets/d9ed502a-625a-4b7c-8aa8-e6f48ae837ef" />


---
<img width="1920" height="1564" alt="image" src="https://github.com/user-attachments/assets/77219e4d-5703-47ab-98d7-5b0b60ed61c8" />

## Features

- Create classes with a name and total seats (for example "Class 6 - Morning Shift").
- Live seat counts for every class: **total, held, confirmed, available**. The UI refreshes every 3 seconds without a page reload.
- Guardians can see how many seats are available when they pick a class. A full class cannot be selected and shows "Class is full".
- A seat is held for a limited time (`HOLD_DURATION_SECONDS`, default 600 seconds) with a **countdown timer** based on the server's `expiresAt`.
- The guardian confirms the admission fee before the timer ends, or cancels. Expired holds release their seat automatically, even if nobody calls the API.
- A student can have only **one active application per class**.
- Every error (class full, hold expired, invalid input, server down) shows a clear message. There are no blank pages.
- All data is stored in PostgreSQL and survives restarts and `docker compose down` / `up`.

---

## Quick start (Docker)

You only need Docker installed.

```bash
docker compose up --build
```

| Service | URL |
|---|---|
| Frontend | http://localhost:3000 |
| Backend API | http://localhost:8080 |
| PostgreSQL | `localhost:5433` (user `postgres`, password `postgres`, database `school_admission`) |

Useful commands:

```bash
# Shorter hold time (30 seconds) to test expiry quickly
HOLD_DURATION_SECONDS=30 docker compose up --build        # Linux / macOS / Git Bash
$env:HOLD_DURATION_SECONDS=30; docker compose up --build   # PowerShell

# Stop (data is kept in the pgdata volume)
docker compose down

# Stop and delete all data
docker compose down -v
```

> If you update from an older version of the schema, run `docker compose down -v` once, because the schema gained a table and a sequence.

---

## How to use the app

Open http://localhost:3000. The home page asks who you are:

### Admin portal (`/#/admin`)
1. Open the **Classes and seats** tab and create a class (name + total seats).
2. Watch seat counts update live (confirmed, held, open).
3. Open the **Applications** tab to approve or reject guardian applications. Approving creates the seat hold and starts the timer. If the guardian misses the payment window, the admin can approve again.

### Guardian portal (`/#/guardian`)
1. Open **New application**, enter guardian name, student name and guardian NID (10, 13 or 17 digits), then choose a class. Each class shows its available seats, and full classes cannot be selected.
2. After you submit, the system generates a **14-digit Student ID** for the child.
3. Open **Track and pay** (it remembers your NID after a refresh). When the admin approves, a countdown appears with **Confirm admission fee** and **Cancel application** buttons.
4. After confirming, the application shows "Admission confirmed".

There is no login. Admin and guardian pages are separated in the UI only, not by security (see [ASSUMPTIONS.md](ASSUMPTIONS.md)).

---

## Application flow

```
Guardian submits application ──► PENDING (no seat held yet)
                                    │
                        Admin approves (or rejects)
                                    ▼
                         Seat HELD + countdown starts
                                    │
          ┌─────────────────────────┼──────────────────────────┐
   Guardian confirms        Guardian cancels            Timer runs out
          ▼                         ▼                          ▼
      CONFIRMED                 CANCELLED                   EXPIRED
                                (seat freed)              (seat freed automatically)
```

The 8 required endpoints work directly on holds. The guardian application and admin approval are additional endpoints built on top of them.

---

## API

Base URL: `http://localhost:8080`. Requests that act as a student need the header `X-Student-Id`. Errors return `{ "error": "message" }`.

### Required endpoints

| # | Method and path | Purpose | Success |
|---|---|---|---|
| 1 | `POST /classes` | Create a class (`name`, `totalSeats`) | 201 |
| 2 | `GET /classes` | List classes with total, held, confirmed and available seats | 200 |
| 3 | `GET /classes/{classId}` | One class and its seat status | 200 |
| 4 | `POST /classes/{classId}/holds` | Apply (hold a seat). Needs `X-Student-Id` | 201, or **409** if no seat is available |
| 5 | `GET /holds/{holdId}` | Current status of a hold | 200 |
| 6 | `POST /holds/{holdId}/confirm` | Confirm admission (idempotent). Needs `X-Student-Id` | 200 |
| 7 | `DELETE /holds/{holdId}` | Cancel a hold. Needs `X-Student-Id` | 204 |
| 8 | `GET /me/holds` | All holds of this student. Needs `X-Student-Id` | 200 |

Hold status is one of `HELD`, `CONFIRMED`, `EXPIRED`, `CANCELLED`.

### Error codes

| Code | Meaning |
|---|---|
| 400 | Missing `X-Student-Id` header or invalid body |
| 403 | The hold belongs to another student |
| 404 | Class, hold or application not found |
| 409 | No seats, duplicate application, or hold expired / not confirmable |
| 500 | Unexpected server error |

### Examples

```bash
# 1. Create a class
curl -X POST http://localhost:8080/classes \
  -H "Content-Type: application/json" \
  -d '{"name":"Class 6 - Morning Shift","totalSeats":120}'
# 201 { "id":"c_1a2b3c4d5e6f", "name":"Class 6 - Morning Shift", "totalSeats":120, "heldSeats":0, "confirmedSeats":0, "availableSeats":120 }

# 2. Apply (hold a seat)
curl -X POST http://localhost:8080/classes/c_1a2b3c4d5e6f/holds \
  -H "X-Student-Id: 20261234567890"
# 201 { "holdId":"h_9f8e7d6c5b4a", "classId":"c_1a2b3c4d5e6f", "studentId":"20261234567890", "status":"HELD", "expiresAt":"2026-11-10T04:10:00Z" }

# 3. Confirm admission
curl -X POST http://localhost:8080/holds/h_9f8e7d6c5b4a/confirm \
  -H "X-Student-Id: 20261234567890"
# 200 { "holdId":"h_9f8e7d6c5b4a", "status":"CONFIRMED" }

# 4. Cancel
curl -X DELETE http://localhost:8080/holds/h_9f8e7d6c5b4a \
  -H "X-Student-Id: 20261234567890"
# 204

# 5. My holds
curl http://localhost:8080/me/holds -H "X-Student-Id: 20261234567890"
```

### Extra endpoints (guardian application and admin approval)

| Method and path | Purpose |
|---|---|
| `POST /applications` | Guardian submits an application (`guardianName`, `studentName`, `nid`, `classId`). Generates the Student ID |
| `GET /applications?nid=` | Applications of one guardian NID |
| `GET /admin/applications` | All applications (admin) |
| `POST /applications/{id}/approve` | Approve: creates the seat hold and starts the timer |
| `POST /applications/{id}/reject` | Reject a pending application |

---

## Business rules

- `held + confirmed` never exceeds `totalSeats`, even with many concurrent requests.
- Only `HELD` holds that have not expired can be confirmed. Confirming twice returns 200 and does not count the seat twice.
- An expired hold cannot be confirmed (409) and its seat becomes available again.
- Cancelling a `CONFIRMED` application is rejected (409). Cancelling an already cancelled hold returns 204.
- A student can have one active application (`HELD` or `CONFIRMED`) per class. After cancel or expiry the student can apply again.
- A pending application does not hold a seat. The seat is held only when the admin approves.

---

## How overselling is prevented

Seat safety is enforced inside **PostgreSQL**, not in application memory, so it also works with several backend processes:

1. Every create-hold, confirm, cancel and approve runs in a transaction that first runs `SELECT ... FROM classes WHERE "Id" = @id FOR UPDATE`. Only one transaction at a time can hold that row lock, so all seat changes for one class are serialized. After the lock is acquired, seats are counted with a fresh read.
2. A **partial unique index** on `(ClassId, StudentId) WHERE Status IN ('HELD','CONFIRMED')` guarantees one active application per student and class, even if application code had a bug.
3. A `CHECK (TotalSeats > 0)` constraint on classes.

No in-process locks (`lock`, `SemaphoreSlim`) are used.

**Automatic expiry:** every count and every response treats a `HELD` hold with `expiresAt <= now` as expired, so seats are free the moment the timer ends. A background service (`ExpiryWorker`, every 5 seconds) also writes `EXPIRED` to the database, even when nobody calls the API.

More details are in [ASSUMPTIONS.md](ASSUMPTIONS.md).

---

## Configuration

| Variable | Used by | Default | Description |
|---|---|---|---|
| `DB_CONNECTION_STRING` | Backend | `Host=localhost;Port=5432;Database=school_admission;Username=postgres;Password=postgres` | PostgreSQL connection string |
| `HOLD_DURATION_SECONDS` | Backend | `600` | How long a seat is held |
| `ASPNETCORE_URLS` | Backend | `http://localhost:8080` (Docker: `http://+:8080`) | Backend listen address |
| `BACKEND_URL` | Frontend container | `http://localhost:8080` | URL the **browser** uses to reach the backend. Injected at start-up through `/config.js` |
| `VITE_API_URL` | Frontend (dev) | `http://localhost:8080` | Backend URL when running `npm run dev` |

---

## Run locally without Docker

Requirements: .NET 8 SDK, Node 20+, PostgreSQL.

**Backend** (from the repository root):

```bash
# Git Bash / Linux / macOS
export DB_CONNECTION_STRING="Host=localhost;Port=5432;Database=school_admission;Username=postgres;Password=YOUR_PASSWORD"
dotnet run --project backend/src/SchoolAdmission.Api
```

```powershell
# PowerShell
$env:DB_CONNECTION_STRING="Host=localhost;Port=5432;Database=school_admission;Username=postgres;Password=YOUR_PASSWORD"
dotnet run --project backend/src/SchoolAdmission.Api
```

Tables are created automatically on start. The backend listens on http://localhost:8080.

**Frontend:**

```bash
cd frontend
npm install
npm run dev      # http://localhost:5173
```

---

## Tests

### Backend

The tests use a **real PostgreSQL**, because row locks cannot be tested with a fake database.

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=school_admission_test;Username=postgres;Password=YOUR_PASSWORD"
dotnet test backend/tests/SchoolAdmission.Tests
```

| Test | What it proves |
|---|---|
| `Concurrent_holds_never_oversell_seats` | 50 simultaneous requests for 10 seats: exactly 10 succeed, 40 get 409 |
| `Concurrent_hold_and_confirm_mix_never_exceeds_total` | Holds and confirms at the same time never exceed the total |
| `Same_student_cannot_hold_twice_even_concurrently` | 15 simultaneous requests from one student: only 1 succeeds |
| `Confirm_is_idempotent_and_owner_only` | Confirming twice counts once; other students get 403 |
| `Cancel_frees_the_seat` | A cancelled hold frees the seat for someone else |
| `Expired_hold_frees_seat_and_cannot_be_confirmed` | Expiry frees the seat and blocks confirm |
| `Missing_student_header_is_rejected` | Missing header returns 400 |
| `Apply_approve_pay_flow_and_seat_limit` | Application, approval and payment flow, and the seat limit on approval |

### Frontend

```bash
cd frontend
npm test
```

---

## Project structure

```
.
├── docker-compose.yml          # PostgreSQL + backend + frontend
├── README.md
├── ASSUMPTIONS.md              # Decisions and assumptions
├── AI_USAGE.md                 # How AI tools were used
├── backend/
│   ├── Dockerfile
│   ├── src/SchoolAdmission.Api/
│   │   ├── Program.cs              # Configuration and endpoints
│   │   ├── AdmissionService.cs     # Classes, holds, confirm, cancel (row locking)
│   │   ├── ApplicationService.cs   # Guardian applications and admin approval
│   │   ├── ExpiryWorker.cs         # Background job that marks holds EXPIRED
│   │   ├── Dtos.cs
│   │   └── Data/                   # EF Core models and DbContext
│   └── tests/SchoolAdmission.Tests/  # Integration and concurrency tests
└── frontend/
    ├── Dockerfile
    ├── nginx.conf.template     # Serves the app and the runtime /config.js
    └── src/
        ├── App.jsx                 # Role chooser and routing
        ├── AdminPortal.jsx         # Create classes, live seat counts
        ├── AdminApplications.jsx   # Approve / reject applications
        ├── GuardianPortal.jsx      # Apply, track and pay
        ├── api.js                  # API client with error handling
        └── hooks.js, utils.js      # Polling, countdown helpers
```

---

## Troubleshooting

| Problem | Fix |
|---|---|
| `port is already allocated` | Another program uses port 3000, 8080 or 5433. Stop it or change the port in `docker-compose.yml`. |
| The page says "Cannot reach the server" | The backend is not running or `BACKEND_URL` is wrong. Check `docker compose ps` and http://localhost:8080/classes. |
| Backend cannot connect to the database when running locally | Check `DB_CONNECTION_STRING` and that PostgreSQL is running. |
| Old data or schema errors after an update | Run `docker compose down -v` to reset the database. |

---

## Documents

- [ASSUMPTIONS.md](ASSUMPTIONS.md): decisions, assumptions, and the database-level concurrency control
- [AI_USAGE.md](AI_USAGE.md): which AI tools were used and how
