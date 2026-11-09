# School Admission System

First-come-first-served school admission: admins create classes with limited seats, students **hold** a seat for a limited time, then **pay** (confirm) or let it expire / cancel.

**Stack:** .NET 8 (minimal API, EF Core) · PostgreSQL 16 · React 18 (Vite) · Docker

## Run with Docker (one command)

```bash
docker compose up --build
```

| Service  | URL                    |
|----------|------------------------|
| Frontend | http://localhost:3000  |
| Backend  | http://localhost:8080  |
| Postgres | localhost:5433 (user/pass `postgres`, db `school_admission`) |

- Hold time: `HOLD_DURATION_SECONDS` (default 600). Example: `HOLD_DURATION_SECONDS=30 docker compose up --build`
- Data lives in the `pgdata` volume and survives `docker compose down` + `up`. (`docker compose down -v` deletes it.)
- Env vars: backend uses `DB_CONNECTION_STRING`, `HOLD_DURATION_SECONDS`; frontend container uses `BACKEND_URL` (the URL the browser uses to reach the backend).

## Run locally without Docker (VS Code)

Requirements: .NET 8 SDK, Node 20+, PostgreSQL (e.g. the one managed with pgAdmin).

**Backend** (PowerShell, from repo root):
```powershell
$env:DB_CONNECTION_STRING="Host=localhost;Port=5432;Database=school_admission;Username=postgres;Password=YOUR_PASSWORD"
dotnet run --project backend/src/SchoolAdmission.Api
```
The tables are created automatically on start (the database itself is created too if your user is allowed to). Listens on http://localhost:8080. To run a second process on another port: `$env:ASPNETCORE_URLS="http://localhost:8081"`.

**Frontend:**
```powershell
cd frontend
npm install
npm run dev      # http://localhost:5173
```

## Tests

Backend tests use a **real PostgreSQL** (row locks can't be tested with a fake). Point them at your Postgres:
```powershell
$env:TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=school_admission_test;Username=postgres;Password=YOUR_PASSWORD"
dotnet test backend/tests/SchoolAdmission.Tests
```
Includes the **concurrency tests** (50 simultaneous requests for 10 seats → exactly 10 succeed, 40 get 409).

Frontend tests: `cd frontend && npm test`

## API

`POST /classes`, `GET /classes`, `GET /classes/{id}`, `POST /classes/{id}/holds`, `GET /holds/{id}`, `POST /holds/{id}/confirm`, `DELETE /holds/{id}`, `GET /me/holds`.
Endpoints that act as a student need the `X-Student-Id` header.
