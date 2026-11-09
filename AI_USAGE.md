# AI usage

## Tools used
- **Claude (Anthropic)**, through claude.ai.

## What I used it for
- **Bug checking:** I asked Claude to review the whole project against the assignment brief and look for bugs and missing requirements. It read the backend (seat locking, expiry, confirm/cancel), the Docker files, the tests, and the React frontend.
- **Environment setup:** Claude helped me with Docker Compose, running the project locally, and publishing the project to GitHub with Git.
- **Small UI change:** I asked Claude to update the guardian class picker so it shows the available seats and blocks full classes with a "Class is full" message.

## What the AI review found, and what I did about it
- The class picker did not show a full state. Guardians could select a class with no seats. I changed it to show the available seats, disable full classes, and show a "Class is full" message (also when a class fills up while the form is open).
- Some frontend components (`ClassList`, `MyApplications`, `AdminPanel`) are no longer wired into the app after I added the guardian application and admin approval flow. I kept them and documented the approval flow as an extension on top of the 8 required endpoints.
- The first version of this file was an empty template. I filled it in.

## AI mistakes I caught
- Claude first told me the brief's Student ID is 17 digits. When I checked the brief again, the text says 14 digits, while the example ID in the API section has 17 digits. The backend accepts any non-empty ID (up to 64 characters), so no code change was needed, and I did not enforce a fixed length in the UI.

## What I checked myself
- I read the seat-locking logic (`SELECT ... FOR UPDATE` on the class row) and the partial unique index that allows only one active application per class and student.
- I ran the project with Docker Compose and tried the flow in the browser.
- I ran the backend concurrency tests and the frontend tests.

I stayed responsible for the final code and decisions. All assumptions are in `ASSUMPTIONS.md`.
