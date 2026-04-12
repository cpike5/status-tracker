# CLAUDE.md

## Project Overview

Status Tracker is a self-hosted status page application built with .NET 9 and Blazor Server. It monitors HTTP endpoints and displays their health on a real-time dashboard.

## Tech Stack

- .NET 9, Blazor Server (InteractiveServer render mode)
- MudBlazor for UI components
- EF Core + PostgreSQL
- FluentValidation, Polly v8, Serilog, ApexCharts

## Key Design Decisions

- **Database-driven configuration** — monitored endpoints and site branding are stored in PostgreSQL, not config files
- **Environment-driven secrets** — OAuth credentials, connection strings, and allowed emails come from env vars
- **Generic/abstract** — no hardcoded names, URLs, or branding in source code. Anyone should be able to fork and deploy without editing code.

## Architecture

- `BackgroundService` runs the health check scheduler
- Polly handles HTTP retry/timeout policies for health checks
- ASP.NET Core Identity with external OAuth providers (Google, Microsoft, GitHub)
- Access controlled via email whitelist
- Serilog with Seq sink (dev) and Elastic APM (prod)

## Documentation

- `docs/requirements.md` — full requirements spec, data model, design principles

## Commands

```bash
# Run locally
dotnet run

# Run tests
dotnet test

# Apply EF migrations
dotnet ef database update

# Docker
docker compose up -d
```
