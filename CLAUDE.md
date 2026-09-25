# Quiz Management — repo notes

ASP.NET Core 9 MVC + EF Core + PostgreSQL. Layers: `BusinessObjects` → `DataAccessObjects` (DbContext, DAOs,
migrations) → `Repositories` → `Services` → `QuizManagement` (web).

## `main` deploys to production

Render auto-deploys `main` (after CI passes) to the public site backed by Neon `production`.
So every change goes through a feature branch and a PR — never push to `main` directly.
Deployment details: `docs/DEPLOY.md`.

## Run it

- SDK is pinned by `global.json` (9.0.313).
- Database: `docker compose up db`, or any PostgreSQL 16+. Config in `QuizManagement/appsettings.Local.json`
  (git-ignored; copy from `appsettings.Local.example.json`). Environment variables override it.
- Schema: `dotnet tool restore` then
  `dotnet ef database update --project DataAccessObjects --startup-project QuizManagement`.
  Demo data: `db/seed-demo.sql`.
- App: `dotnet run --project QuizManagement --launch-profile http` → http://localhost:5039. Emails go to the
  console (`Email:Provider=Log`).
- Tests: `dotnet test QuizManagementSystem.slnx`. Integration tests (`Category=PostgresIntegration`) need
  PostgreSQL on localhost:5432 (postgres/postgres) or `QUIZ_TEST_POSTGRES_CONNECTION`.

## Conventions

- Schema changes: add an EF migration in the same commit as the model change. A test fails if the model and
  migrations drift apart.
- PostgreSQL compares text case-sensitively: look users/subjects/decks up by the `Normalized*` columns
  (`TextNormalization.Normalize`), never by raw `Email`/`Username`/`Name` equality.
- Cloud-only behaviour (proxy headers, `PORT`, public URL, email provider) is switched on by configuration;
  defaults must keep local development working unchanged.
