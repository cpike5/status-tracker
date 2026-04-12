# Status Tracker

A self-hosted status page for monitoring your projects and services. Deploy it on a VPS, configure your endpoints, and get a clean real-time dashboard showing what's up and what's down.

## Features

- **Real-time dashboard** — live uptime percentages, response time charts, and 90-day history timelines via SignalR
- **HTTP health checks** — configurable intervals per endpoint, Polly retry/timeout policies, concurrent execution
- **Response time charts** — ApexCharts sparklines and trend graphs per endpoint
- **Uptime history** — rolling percentage calculations stored in PostgreSQL
- **OAuth authentication** — Google, Microsoft, and GitHub providers; enable whichever you need
- **Email whitelist** — restrict access to a comma-separated list of addresses
- **Database-driven configuration** — add and manage monitored endpoints via the admin UI; no config file edits required
- **Customizable branding** — site title, logo URL, accent color, and footer text adjustable at runtime from the admin UI
- **Data retention** — automatic scheduled pruning of old check results (default: 90 days, configurable)
- **Fork-and-deploy** — clone, set env vars, `docker compose up`

## Tech Stack

.NET 9 | Blazor Server | MudBlazor | EF Core | PostgreSQL | Polly | Serilog | ApexCharts

## Quick Start

### Prerequisites

- Docker and Docker Compose
- OAuth client credentials for at least one provider (Google, Microsoft, or GitHub)

### 1. Clone

```bash
git clone https://github.com/your-username/status-tracker.git
cd status-tracker
```

### 2. Configure

Copy the example environment file and fill in your values:

```bash
cp .env.example .env
# Edit .env with your values (see docs/configuration.md)
```

Minimum required variables:

```env
POSTGRES_PASSWORD=your-db-password
ALLOWED_EMAILS=you@example.com

# Enable at least one OAuth provider
GOOGLE_CLIENT_ID=...
GOOGLE_CLIENT_SECRET=...
```

### 3. Run

```bash
docker compose up -d
```

The app will be available at `http://localhost:5000`.

On first launch, EF Core migrations run automatically and the database schema is created. Log in with one of the configured OAuth providers and start adding endpoints via the admin UI.

## Configuration

All configuration is done through environment variables — no source code changes needed.

| Variable | Description | Required |
|---|---|---|
| `POSTGRES_PASSWORD` | PostgreSQL password | Yes |
| `ALLOWED_EMAILS` | Comma-separated login whitelist | Yes |
| `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` | Google OAuth credentials | No* |
| `MICROSOFT_CLIENT_ID` / `MICROSOFT_CLIENT_SECRET` | Microsoft OAuth credentials | No* |
| `GITHUB_CLIENT_ID` / `GITHUB_CLIENT_SECRET` | GitHub OAuth credentials | No* |
| `HealthCheck__DefaultIntervalSeconds` | How often to poll each endpoint (default: `60`) | No |
| `DataRetention__RetentionDays` | Days to retain check results (default: `90`) | No |

\* At least one OAuth provider must be configured.

See [docs/configuration.md](docs/configuration.md) for the complete variable reference, including health check tuning, data retention, observability (Seq, Elastic APM), and Docker Compose integration details.

## Local Development

Requires the .NET 9 SDK, a local PostgreSQL instance, and (optionally) Seq for log viewing.

```bash
# Set the connection string and at least one OAuth provider
export ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=statustracker;Username=app;Password=dev"
export ASPNETCORE_ENVIRONMENT=Development
export GOOGLE_CLIENT_ID=...
export GOOGLE_CLIENT_SECRET=...

# Apply migrations and run
dotnet ef database update --project src/StatusTracker
dotnet run --project src/StatusTracker
```

In `Development` mode the email whitelist check and OAuth requirement are relaxed, which allows the app to start without all credentials configured.

## Testing

```bash
# Run all tests
dotnet test

# Run only unit tests (no Docker required)
dotnet test --filter "Category=Unit"

# Run only integration tests (requires Docker for Testcontainers)
dotnet test --filter "Category=Integration"
```

Test layout:

```
tests/StatusTracker.Tests/
  Unit/         — fast, in-process tests with NSubstitute mocks
  Integration/  — database tests using Testcontainers (spins up a real PostgreSQL container)
```

## Architecture

Status Tracker uses a `BackgroundService` scheduler that polls endpoints on configurable intervals. Results are written to PostgreSQL and broadcast to connected Blazor clients over SignalR. Authentication is handled by ASP.NET Core Identity with external OAuth providers; access is gated by an email whitelist middleware.

See [docs/architecture.md](docs/architecture.md) for the full system design including C4 diagrams, component breakdown, and architecture decision records.

## Documentation

| Document | Description |
|----------|-------------|
| [docs/configuration.md](docs/configuration.md) | Full environment variable reference |
| [docs/architecture.md](docs/architecture.md) | System design, deployment model, ADRs |
| [docs/requirements.md](docs/requirements.md) | Feature spec and data model |
| [docs/prd.md](docs/prd.md) | Product requirements document |
| [docs/brd.md](docs/brd.md) | Business requirements document |
| [docs/design-system.md](docs/design-system.md) | UI component and theming guide |
| [docs/user-stories.md](docs/user-stories.md) | User stories and acceptance criteria |

## License

[GPL-3.0](LICENSE)
