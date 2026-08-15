# Financial Planning App

Razor Pages + Dapper + MariaDB MVP scaffold generated from the submitted master prompt.

## Included in this scaffold

- .NET 10 Razor Pages web app
- Layered folders (`Data`, `Services`, `Infrastructure`, `BackgroundServices`)
- Cookie authentication with user registration/login
- Per-user recurring payment templates
- Annual plan summary service
- Startup schema migration hosted service
- Serilog console logging
- Dockerfile + Docker Compose (web + MariaDB)

## Run locally

1. Start MariaDB:

```bash
docker compose up -d db
```

2. Copy `FinancialPlanningApp.Web/secrets.template.json` to `FinancialPlanningApp.Web/secrets.json` and fill in the database values.
3. Run app:

```bash
dotnet run --project FinancialPlanningApp.Web
```

## Run full stack

```bash
docker compose up --build
```

App: http://localhost:8080

For Docker Compose, copy `.env.example` to `.env` and fill in the database values first.

## Next implementation slices

1. Complete CRUD for templates (edit, delete, archive, filtering, pagination)
2. Introduce DbUp/FluentMigrator versioned migrations
3. Add payment tracking + corrections tables
4. Add CSV import pipeline (parser -> normalization -> matching -> reconciliation)
5. Add CODA and PDF provider-based import adapters
6. Add yearly snapshot versioning and forecast charts
