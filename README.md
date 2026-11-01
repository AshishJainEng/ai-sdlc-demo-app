# Invoice Approval Demo App

A small invoice approval app built for AI-in-SDLC demos. It is intentionally simple: a .NET 8 Web API, a static HTML/JavaScript UI, and an in-memory repository so you can run it locally without a database.

## What this app demonstrates

The demo covers the invoice lifecycle and approval policy enforcement:

- Create and submit invoices
- Move invoices through Draft and Pending Approval
- Approve or reject invoices with a reason
- Enforce approval authority limits by role
- Require multiple approvers for large-value invoices
- Prevent the same user from acting as both approvers
- Keep invoice status unchanged on rejection

## Business rules implemented

- Manager approval limit: 50,000
- Director approval limit: 500,000
- Invoices over 500,000 require two distinct approvers
- The second approver on large invoices must be a Director
- The same approver cannot appear twice on the same invoice
- Rejections return a reason string and do not mutate the invoice state

## Run the app

```bash
dotnet run --project backend/src/InvoiceApp.Api
```

Open http://localhost:5000 in the browser.

Sign in with one of the demo accounts:

| User | Password | Role |
| --- | --- | --- |
| `demo.approver` | `ChangeMe123!` | Approver |
| `demo.admin` | `ChangeMe123!` | Admin |

These are demo accounts for a local app only and are not real credentials.

## Test the app

Run the .NET unit tests:

```bash
dotnet test backend/InvoiceApp.sln
```

Run the Playwright end-to-end tests:

```bash
cd e2e
npm install
npx playwright install chromium
npm test
```

## Project layout

```text
backend/
  src/
    InvoiceApp.Domain      business rules and approval logic
    InvoiceApp.Api         controllers, authentication, in-memory repository, web UI
  tests/
    InvoiceApp.Tests       xUnit unit tests

e2e/
  tests/                   Playwright end-to-end tests
  pages/                   page objects for UI flows

docs/
  use-cases/               feature briefs and examples

AGENTS.md                  conventions for AI coding agents
README.md                  project overview and run instructions
```

## Use cases

- [UC-1: Bulk invoice approval](docs/use-cases/UC-1-bulk-approval.md)
- [UC-2: Partial approval](docs/use-cases/UC-2-partial-approval.md)
- [UC-3: Approval limits](docs/use-cases/UC-3-approval-limits.md)

## Notes

This repository is structured around a clean separation between domain logic and API concerns:

- Domain rules live in `backend/src/InvoiceApp.Domain`
- Controllers and in-memory storage live in `backend/src/InvoiceApp.Api`
- UI assets are served from `backend/src/InvoiceApp.Api/wwwroot`
- Tests are split between unit tests and browser-level E2E tests
