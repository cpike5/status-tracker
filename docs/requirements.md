# Status Tracker - Requirements

A self-hosted, generic status page application for monitoring projects and services. Similar in concept to public status pages run by major tech companies, but designed for anyone to deploy and monitor their own infrastructure. The application is fully configurable — endpoints, branding, and behavior are all driven by database and environment configuration, so a new user can fork/clone, configure, and deploy without modifying source code.

## Tech Stack

- **.NET 9** (Blazor Server, InteractiveServer render mode)
- **MudBlazor** for UI components (Material Design)
- **Entity Framework Core** with **PostgreSQL**
- **FluentValidation** for input validation
- **Polly v8** for HTTP retry/timeout policies on health checks
- **Serilog** for structured logging (Seq sink for local dev)
- **BackgroundService** for health check scheduling
- **ApexCharts** (Blazor wrapper) for uptime/response-time visualizations

## Design Principles

- **Generic by default:** No hardcoded project names, URLs, or branding in source code. Everything is configuration.
- **Database-driven endpoint config:** All monitored endpoints are rows in the database. No YAML/JSON files to maintain — manage endpoints through the admin UI or direct DB access.
- **Environment-driven secrets:** OAuth client IDs, DB connection strings, and app settings come from environment variables (or appsettings overrides), never source code.
- **Fork-and-deploy:** A new user should be able to: clone the repo, set env vars, run `docker compose up`, and have a working status page.
- **Configurable branding:** Site title, logo URL, accent color, and footer text are stored in a `SiteSettings` table (seeded with sensible defaults, editable at runtime).

## Authentication

- OAuth-based login required to access the application (no anonymous access to admin features)
- **Supported providers (configurable):** Google, Microsoft, GitHub — enabled/disabled via environment variables
- Use ASP.NET Core Identity + external login providers
- Allowed users controlled via a whitelist (email addresses in DB or env var) — not open registration
- Which OAuth providers are active is determined by which client ID/secret env vars are present

## Core Features

### 1. Endpoint Management (CRUD)

Authenticated users can add, edit, and remove monitored endpoints via the admin UI. Endpoints can also be seeded or managed directly in the database for infrastructure-as-config workflows.

Each endpoint has:
- **Name** - friendly display name (e.g., "Portfolio Site", "API Gateway")
- **Group** (optional) - logical grouping for dashboard organization (e.g., "Production", "Staging")
- **URL** - the HTTP(S) endpoint to monitor
- **Check interval** - how often to poll (e.g., 30s, 1m, 5m)
- **Expected status code** - default 200, configurable
- **Expected response body** (optional) - substring or regex match on response body
- **Timeout** - max wait time before marking as down (default 10s)
- **Sort order** - controls display position on dashboard
- **Enabled/Disabled** toggle

### 2. Health Check Engine

A background service that runs health checks on all enabled endpoints.

Logic:
- Send HTTP GET (or HEAD) to the endpoint URL
- **Pass** if response status matches expected code AND (if configured) body matches expected content
- **Fail** if request times out, connection refused, or status/body mismatch
- Record each check result with timestamp, response time (ms), status code, and pass/fail
- Retry logic: configurable number of retries before marking as down (default: 2 consecutive failures)

### 3. Status Dashboard

The primary UI - a real-time dashboard showing the current state of all monitored endpoints.

- List of all endpoints with current status (Up / Down / Degraded / Unknown)
- Response time for last successful check
- Uptime percentage (24h, 7d, 30d)
- Uptime history timeline (visual bar showing up/down over time, similar to GitHub/Atlassian status pages)
- Last checked timestamp
- Auto-refresh via SignalR (Blazor Server)

### 4. Public Status Page (Future)

- Optional unauthenticated read-only view of selected endpoints
- Configurable per-endpoint visibility (public / private)

## Data Model (High Level)

### SiteSettings
- Id, SiteTitle, LogoUrl, AccentColor, FooterText
- (Single row, seeded with defaults on first run)

### MonitoredEndpoint
- Id, Name, Group, Url, CheckIntervalSeconds, ExpectedStatusCode, ExpectedBodyMatch
- TimeoutSeconds, RetryCount, IsEnabled, IsPublic, SortOrder
- CreatedAt, UpdatedAt

### CheckResult
- Id, EndpointId (FK), Timestamp, ResponseTimeMs
- HttpStatusCode, IsHealthy, ErrorMessage
- (Partitioned or pruned by age - keep 90 days by default)

### AppUser
- Extends ASP.NET Core IdentityUser
- Links to OAuth external login

## Non-Functional Requirements

- **Deployment:** Docker Compose stack (app container + PostgreSQL + Seq). Follows the project's standard compose template with external elastic network for APM.
- **Configuration:** All runtime config via environment variables or appsettings overrides. Zero source code changes needed to deploy your own instance.
- **Performance:** Health checks run in parallel via Polly; staggered to avoid thundering herd
- **Storage:** Auto-prune check results older than configurable retention period (default 90 days)
- **Logging:** Serilog with Seq sink (dev) and Elastic APM integration (prod)

## Out of Scope (v1)

- Email / Slack / webhook notifications (dashboard-only alerting for now)
- TCP / ICMP / custom script health checks
- Multi-user teams / RBAC
- Incident management / manual status overrides
- SSL certificate expiry monitoring

## Future Considerations

- Notification channels (email, Discord/Slack webhooks)
- Additional check types (TCP, ping, custom scripts)
- Public status page with custom domain
- Incident timeline / manual status overrides
- Mobile-friendly responsive design improvements
