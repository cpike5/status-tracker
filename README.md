# Status Tracker

A self-hosted status page for monitoring your projects and services. Deploy it on a VPS, configure your endpoints, and get a clean dashboard showing what's up and what's down.

## Features

- **Real-time dashboard** with uptime percentages, response times, and history timelines
- **HTTP health checks** running on configurable intervals with retry logic
- **OAuth login** (Google, Microsoft, GitHub) — enable whichever providers you need
- **Database-driven config** — manage endpoints via the admin UI or directly in PostgreSQL
- **Customizable branding** — site title, logo, colors, and footer text configurable at runtime
- **Fork-and-deploy** — clone, set env vars, `docker compose up`

## Tech Stack

.NET 9 | Blazor Server | MudBlazor | EF Core | PostgreSQL | Polly | Serilog | ApexCharts

## Quick Start

### Prerequisites

- Docker & Docker Compose
- OAuth client credentials for at least one provider (Google, Microsoft, or GitHub)

### 1. Clone

```bash
git clone https://github.com/cpike5/status-tracker.git
cd status-tracker
```

### 2. Configure

Copy the example environment file and fill in your values:

```bash
cp .env.example .env
```

Required variables:

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

## Configuration

All configuration is done through environment variables — no source code changes needed.

| Variable | Description | Required |
|---|---|---|
| `POSTGRES_PASSWORD` | PostgreSQL password | Yes |
| `ALLOWED_EMAILS` | Comma-separated email whitelist | Yes |
| `GOOGLE_CLIENT_ID` / `SECRET` | Google OAuth credentials | No* |
| `MICROSOFT_CLIENT_ID` / `SECRET` | Microsoft OAuth credentials | No* |
| `GITHUB_CLIENT_ID` / `SECRET` | GitHub OAuth credentials | No* |

\* At least one OAuth provider must be configured.

## Documentation

- [Requirements](docs/requirements.md) — full feature spec, data model, and design principles

## License

[GPL-3.0](LICENSE)
