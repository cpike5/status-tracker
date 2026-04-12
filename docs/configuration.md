# Configuration Reference

## Status Tracker

**Version:** 1.0  
**Date:** 2026-04-12  
**Status:** Draft

---

## Overview

Status Tracker is configured entirely through environment variables and `appsettings.json` overrides. No source code changes are required to deploy your own instance.

There are two layers of configuration:

| Layer | How it works | Typical use |
|-------|-------------|-------------|
| **Environment variables** | Injected by Docker Compose from your `.env` file | Secrets, credentials, deployment-specific values |
| **`appsettings.json` overrides** | ASP.NET Core nested key format using `__` separator | Tuning health check intervals, retention periods, log levels |

See [ASP.NET Core Environment Variable Mapping](#aspnet-core-environment-variable-mapping) for how these two layers interact.

---

## Quick Reference

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `POSTGRES_PASSWORD` | Yes | — | PostgreSQL password |
| `ALLOWED_EMAILS` | Yes (non-Dev) | — | Comma-separated login whitelist |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` | Runtime environment |
| `GOOGLE_CLIENT_ID` | No* | — | Google OAuth client ID |
| `GOOGLE_CLIENT_SECRET` | No* | — | Google OAuth client secret |
| `MICROSOFT_CLIENT_ID` | No* | — | Microsoft OAuth client ID |
| `MICROSOFT_CLIENT_SECRET` | No* | — | Microsoft OAuth client secret |
| `GITHUB_CLIENT_ID` | No* | — | GitHub OAuth client ID |
| `GITHUB_CLIENT_SECRET` | No* | — | GitHub OAuth client secret |
| `SEQ_PASSWORD` | No | (none) | Seq admin password hash |
| `ElasticApm__ServerUrl` | No | (APM disabled) | Elastic APM server URL |
| `ElasticApm__ServiceName` | No | `status-tracker` | APM service name |
| `ElasticApm__Environment` | No | `development` | APM environment tag |
| `HealthCheck__DefaultIntervalSeconds` | No | `60` | Default check interval |
| `HealthCheck__DefaultTimeoutSeconds` | No | `10` | Default HTTP timeout |
| `HealthCheck__DefaultRetryCount` | No | `2` | Default retry count |
| `HealthCheck__MaxConcurrency` | No | `10` | Max parallel checks |
| `DataRetention__RetentionDays` | No | `90` | Check result retention |
| `DataRetention__PruneSchedule` | No | `0 2 * * *` | Prune cron schedule |

\* At least one OAuth provider (both `CLIENT_ID` and `CLIENT_SECRET`) must be configured in Production.

---

## Required Variables

### `POSTGRES_PASSWORD`

**Type:** String  
**Required:** Yes  

The password for the PostgreSQL `app` database user. This value is used in two places:

1. The `postgres` container sets it as the user password via `POSTGRES_PASSWORD`.
2. The `status-tracker` container receives a fully constructed connection string:
   ```
   Host=postgres;Port=5432;Database=statustracker;Username=app;Password=<POSTGRES_PASSWORD>
   ```

The connection string is assembled in `docker-compose.yml` and passed to the application as `ConnectionStrings__Postgres`, which ASP.NET Core maps to `ConnectionStrings:Postgres`. The application fails fast at startup if this connection string is absent in Production.

**Example:**
```env
POSTGRES_PASSWORD=s3cur3-p@ssw0rd
```

---

### `ALLOWED_EMAILS`

**Type:** Comma-separated string  
**Required:** Yes (when `ASPNETCORE_ENVIRONMENT` is not `Development`)  

A comma-separated list of email addresses permitted to log in. Anyone who authenticates via OAuth but whose email is not on this list is redirected to `/access-denied`.

`Program.cs` manually maps this env var to the `Auth:AllowedEmails` configuration key. Spaces around commas are trimmed automatically.

**Examples:**
```env
# Single user
ALLOWED_EMAILS=admin@example.com

# Multiple users
ALLOWED_EMAILS=alice@example.com,bob@example.com, carol@example.com
```

---

## Application Runtime

### `ASPNETCORE_ENVIRONMENT`

**Type:** String  
**Required:** No  
**Default:** `Production`  

Controls the ASP.NET Core runtime environment. The two meaningful values are:

| Value | Behavior |
|-------|----------|
| `Production` | Exception handler middleware active; fail-fast startup validation enforced; Elastic APM enabled if configured |
| `Development` | Developer exception page enabled; fail-fast validation skipped (allows running without OAuth credentials); Seq sink used for local log viewing |

**Example:**
```env
ASPNETCORE_ENVIRONMENT=Development
```

---

## Authentication (OAuth)

OAuth providers are enabled dynamically. If both `CLIENT_ID` and `CLIENT_SECRET` are set for a provider, that provider appears on the login page and is accepted during authentication. Providers with missing or empty credentials are ignored.

At least one provider must be configured when `ASPNETCORE_ENVIRONMENT` is not `Development`. The application throws an `InvalidOperationException` at startup if no providers are registered in Production.

### OAuth Callback URLs

Register these redirect URIs in each provider's developer console **before** configuring credentials. The path suffix is fixed by the ASP.NET Core OAuth middleware:

| Provider | Callback URL |
|----------|-------------|
| Google | `https://<your-domain>/signin-google` |
| Microsoft | `https://<your-domain>/signin-microsoft` |
| GitHub | `https://<your-domain>/signin-github` |

For local development:
```
https://localhost:<port>/signin-google
https://localhost:<port>/signin-microsoft
https://localhost:<port>/signin-github
```

> Note: Most OAuth providers require HTTPS callback URLs even in development. Use a tool such as `dotnet dev-certs` or a local reverse proxy.

---

### Google OAuth

**Console:** [Google Cloud Console — Credentials](https://console.cloud.google.com/apis/credentials)  
**Callback URL:** `https://<your-domain>/signin-google`

| Variable | Type | Required | Description |
|----------|------|----------|-------------|
| `GOOGLE_CLIENT_ID` | String | No* | OAuth 2.0 client ID from Google Cloud Console |
| `GOOGLE_CLIENT_SECRET` | String | No* | OAuth 2.0 client secret |

The application requests the `email` scope. Both variables must be set to enable this provider.

**Example:**
```env
GOOGLE_CLIENT_ID=123456789-abc.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=GOCSPX-xxxxxxxxxxxxxxxxxxxxxxxx
```

---

### Microsoft OAuth

**Console:** [Azure Portal — App Registrations](https://portal.azure.com/#blade/Microsoft_AAD_RegisteredApps)  
**Callback URL:** `https://<your-domain>/signin-microsoft`

| Variable | Type | Required | Description |
|----------|------|----------|-------------|
| `MICROSOFT_CLIENT_ID` | String | No* | Application (client) ID from Azure App Registration |
| `MICROSOFT_CLIENT_SECRET` | String | No* | Client secret value |

Both variables must be set to enable this provider.

**Example:**
```env
MICROSOFT_CLIENT_ID=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
MICROSOFT_CLIENT_SECRET=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

---

### GitHub OAuth

**Console:** [GitHub — Developer Settings — OAuth Apps](https://github.com/settings/developers)  
**Callback URL:** `https://<your-domain>/signin-github`

| Variable | Type | Required | Description |
|----------|------|----------|-------------|
| `GITHUB_CLIENT_ID` | String | No* | Client ID from the GitHub OAuth App |
| `GITHUB_CLIENT_SECRET` | String | No* | Client secret |

The application requests the `user:email` scope. Both variables must be set to enable this provider.

**Example:**
```env
GITHUB_CLIENT_ID=Iv1.xxxxxxxxxxxx
GITHUB_CLIENT_SECRET=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

---

## Observability

### Serilog / Seq

Serilog is configured via `appsettings.json` (and `appsettings.Development.json`). The Seq sink is active in Development by default, pointing to `http://localhost:5341`. In the Docker Compose stack, the internal URL `http://seq:5341` is injected automatically as `Seq__ServerUrl`.

| Variable | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `SEQ_PASSWORD` | String | No | (none) | Bcrypt hash of the Seq admin UI password. If empty, Seq starts without password protection. |

**Generating a Seq password hash:**
```bash
echo 'YourPassword' | docker run --rm -i datalust/seq:latest seq config --print-input-password-hash
```

The Seq container is accessible at:
- **API / ingest:** `http://localhost:5341` (also used by the application)
- **Web UI:** `http://localhost:8081`

---

### Elastic APM

Elastic APM is enabled only when `ElasticApm__ServerUrl` is non-empty. When enabled, the application uses the `Elastic.Apm.NetCoreAll` package to instrument all requests and background operations.

The application container attaches to the external `elastic` Docker network defined in `docker-compose.yml`. This network must exist before `docker compose up` is run — Docker Compose cannot create external networks automatically.

If you are using Elastic APM, create the network once:

```bash
docker network create elastic
```

If you are **not** using Elastic APM, leave `ElasticApm__ServerUrl` empty in your `.env` file and also remove the `elastic` entries from `docker-compose.yml` (the `networks:` block at the bottom and the `elastic` entry under `status-tracker.networks`) to prevent a startup error.

| Variable | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `ElasticApm__ServerUrl` | String | No | (APM disabled) | URL of the Elastic APM server (e.g., `http://apm-server:8200`) |
| `ElasticApm__ServiceName` | String | No | `status-tracker` | Service name reported to APM |
| `ElasticApm__Environment` | String | No | `development` | Environment tag reported to APM (e.g., `production`) |

**Example `.env` entries:**
```env
ElasticApm__ServerUrl=http://apm-server:8200
ElasticApm__ServiceName=status-tracker
ElasticApm__Environment=production
```

> Note: These variables use the `__` separator directly (not a simple name like `ELASTIC_APM_URL`) because they must map to nested ASP.NET Core configuration keys. See [ASP.NET Core Environment Variable Mapping](#aspnet-core-environment-variable-mapping).

---

## Health Check Configuration

These settings control the behavior of the background health check engine. They are defined in `appsettings.json` under the `HealthCheck` section and can be overridden per deployment via environment variables using the `__` separator.

| Variable | appsettings key | Type | Default | Description |
|----------|----------------|------|---------|-------------|
| `HealthCheck__DefaultIntervalSeconds` | `HealthCheck:DefaultIntervalSeconds` | Integer | `60` | How often to poll each endpoint (seconds) when no per-endpoint interval is set |
| `HealthCheck__DefaultTimeoutSeconds` | `HealthCheck:DefaultTimeoutSeconds` | Integer | `10` | HTTP request timeout (seconds) before a check is considered failed |
| `HealthCheck__DefaultRetryCount` | `HealthCheck:DefaultRetryCount` | Integer | `2` | Number of Polly retries before recording a failed result |
| `HealthCheck__MaxConcurrency` | `HealthCheck:MaxConcurrency` | Integer | `10` | Maximum number of health checks that can run in parallel |

**Notes:**
- Per-endpoint `CheckIntervalSeconds`, `TimeoutSeconds`, and `RetryCount` values stored in the database take precedence over these defaults when set.
- With 2 retries and a 10-second timeout, the worst-case elapsed time before a failure is confirmed is approximately 30 seconds.
- Increasing `MaxConcurrency` is appropriate if you monitor many endpoints and need faster aggregate check cycles.

**Example:**
```env
HealthCheck__DefaultIntervalSeconds=30
HealthCheck__MaxConcurrency=20
```

---

## Data Retention Configuration

These settings control the scheduled pruning of `CheckResult` rows. They are defined in `appsettings.json` under the `DataRetention` section.

| Variable | appsettings key | Type | Default | Description |
|----------|----------------|------|---------|-------------|
| `DataRetention__RetentionDays` | `DataRetention:RetentionDays` | Integer | `90` | Number of days to retain check results. Rows older than this are deleted on each prune run. |
| `DataRetention__PruneSchedule` | `DataRetention:PruneSchedule` | Cron string | `0 2 * * *` | Cron expression controlling when the prune job runs. Default is 2:00 AM UTC daily. |

**Storage estimate:** At one check per minute per endpoint, 50 endpoints with a 90-day retention period produce approximately 6.5 million rows at steady state.

**Example:**
```env
# Keep 180 days, prune at 3 AM UTC
DataRetention__RetentionDays=180
DataRetention__PruneSchedule=0 3 * * *
```

---

## ASP.NET Core Environment Variable Mapping

ASP.NET Core's built-in environment variable configuration provider translates double underscores (`__`) in variable names to colons (`:`) when mapping to configuration keys. This allows nested `appsettings.json` structures to be overridden from the environment without special handling.

**Rule:** Replace `:` in the configuration key path with `__` to form the environment variable name.

| Configuration key | Environment variable |
|-------------------|---------------------|
| `ConnectionStrings:Postgres` | `ConnectionStrings__Postgres` |
| `HealthCheck:DefaultIntervalSeconds` | `HealthCheck__DefaultIntervalSeconds` |
| `DataRetention:RetentionDays` | `DataRetention__RetentionDays` |
| `ElasticApm:ServerUrl` | `ElasticApm__ServerUrl` |

**Exception — ALLOWED_EMAILS:** This variable does not follow the `__` convention. `Program.cs` contains explicit code to read `ALLOWED_EMAILS` and write its value to the `Auth:AllowedEmails` key before the configuration pipeline is finalized:

```csharp
if (builder.Configuration["ALLOWED_EMAILS"] is { } allowedEmails)
{
    builder.Configuration["Auth:AllowedEmails"] = allowedEmails;
}
```

**OAuth provider variables:** The `.env` file uses human-readable names (`GOOGLE_CLIENT_ID`, `MICROSOFT_CLIENT_ID`, `GITHUB_CLIENT_ID`, and their `_SECRET` counterparts). `docker-compose.yml` remaps these to the `__`-separated form that ASP.NET Core requires (`Google__ClientId`, `Microsoft__ClientId`, `GitHub__ClientId`, etc.) so that they resolve correctly to `Google:ClientId`, `Microsoft:ClientId`, and `GitHub:ClientId` in `Program.cs`.

---

## Docker Compose Integration

The `docker-compose.yml` file reads your `.env` file automatically when you run `docker compose up`. It constructs and injects the following values into the application container:

| Container env var | Source | Maps to config key |
|-------------------|--------|--------------------|
| `ASPNETCORE_ENVIRONMENT` | `${ASPNETCORE_ENVIRONMENT:-Production}` | (ASP.NET Core built-in) |
| `ConnectionStrings__Postgres` | Constructed from `${POSTGRES_PASSWORD}` | `ConnectionStrings:Postgres` |
| `Seq__ServerUrl` | Hardcoded to `http://seq:5341` (internal network) | `Seq:ServerUrl` |
| `ALLOWED_EMAILS` | `${ALLOWED_EMAILS}` | Manually mapped to `Auth:AllowedEmails` in `Program.cs` |
| `Google__ClientId` | `${GOOGLE_CLIENT_ID}` | `Google:ClientId` |
| `Google__ClientSecret` | `${GOOGLE_CLIENT_SECRET}` | `Google:ClientSecret` |
| `Microsoft__ClientId` | `${MICROSOFT_CLIENT_ID}` | `Microsoft:ClientId` |
| `Microsoft__ClientSecret` | `${MICROSOFT_CLIENT_SECRET}` | `Microsoft:ClientSecret` |
| `GitHub__ClientId` | `${GITHUB_CLIENT_ID}` | `GitHub:ClientId` |
| `GitHub__ClientSecret` | `${GITHUB_CLIENT_SECRET}` | `GitHub:ClientSecret` |
| `ElasticApm__ServerUrl` | `${ElasticApm__ServerUrl:-}` (empty by default) | `ElasticApm:ServerUrl` |
| `ElasticApm__ServiceName` | `${ElasticApm__ServiceName:-status-tracker}` | `ElasticApm:ServiceName` |
| `ElasticApm__Environment` | `${ElasticApm__Environment:-development}` | `ElasticApm:Environment` |

Values with `:-` provide a fallback when the `.env` variable is absent. The connection string is always assembled from `POSTGRES_PASSWORD` — there is no way to set the full connection string via the `.env` file directly; use `ConnectionStrings__Postgres` if running outside of Docker Compose.

---

## Related Documentation

| Document | Path | Description |
|----------|------|-------------|
| Architecture | `docs/architecture.md` | System design, deployment model, ADRs |
| Requirements | `docs/requirements.md` | Full feature spec and data model |
| README | `README.md` | Quick start guide |
