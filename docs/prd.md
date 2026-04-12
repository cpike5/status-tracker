# Product Requirements Document (PRD)

## Status Tracker

**Version:** 1.0
**Date:** 2026-04-11
**Status:** Draft

---

## 1. Product Overview

Status Tracker is a self-hosted status page application that monitors HTTP endpoints and displays their health on a real-time dashboard. It is built with .NET 9 and Blazor Server, uses MudBlazor for the UI, and stores all configuration in PostgreSQL.

The product is designed around a **fork-and-deploy** workflow: clone the repo, set environment variables, run `docker compose up`, and have a working status page with zero source code changes.

## 2. Target Users

### Primary: Self-Hosting Developer / DevOps Engineer
- Runs personal or small-team infrastructure
- Wants uptime visibility without SaaS costs or vendor lock-in
- Comfortable with Docker, environment variables, and OAuth app registration
- Values simplicity and a clean UI over enterprise feature breadth

### Secondary: Small Team Lead
- Needs a shared dashboard for team services
- Controls access via email whitelist
- Wants custom branding to match their organization

## 3. User Experience Goals

| Goal | Description |
|------|-------------|
| **Zero-config deployment** | Working status page with `docker compose up` and env vars only |
| **Immediate value** | Add an endpoint, see its status within one check interval |
| **Real-time feedback** | Dashboard updates automatically via SignalR, no manual refresh |
| **Clean, information-dense UI** | MudBlazor Material Design with clear status indicators and charts |
| **Low maintenance** | Automatic data pruning, resilient health checks via Polly |

## 4. Feature Requirements

### 4.1 Authentication & Authorization

| ID | Requirement | Priority |
|----|-------------|----------|
| AUTH-1 | Support OAuth login via Google, Microsoft, and GitHub | Must |
| AUTH-2 | Enable/disable providers based on presence of env var credentials | Must |
| AUTH-3 | Restrict access to whitelisted email addresses | Must |
| AUTH-4 | Use ASP.NET Core Identity with external login providers | Must |
| AUTH-5 | Redirect unauthenticated users to login page | Must |
| AUTH-6 | Display available OAuth providers on the login page dynamically | Should |

### 4.2 Endpoint Management

| ID | Requirement | Priority |
|----|-------------|----------|
| EP-1 | CRUD operations for monitored endpoints via admin UI | Must |
| EP-2 | Fields: Name, Group, URL, Check Interval, Expected Status Code, Expected Body Match, Timeout, Retry Count, Sort Order, Enabled toggle | Must |
| EP-3 | Validate endpoint configuration with FluentValidation | Must |
| EP-4 | Support optional grouping for dashboard organization | Should |
| EP-5 | Support database seeding for infrastructure-as-config workflows | Should |
| EP-6 | Display confirmation before deleting an endpoint | Must |

### 4.3 Health Check Engine

| ID | Requirement | Priority |
|----|-------------|----------|
| HC-1 | BackgroundService polls all enabled endpoints at their configured intervals | Must |
| HC-2 | Send HTTP GET to endpoint URL and evaluate response | Must |
| HC-3 | Pass: status code matches expected AND body matches (if configured) | Must |
| HC-4 | Fail: timeout, connection refused, or status/body mismatch | Must |
| HC-5 | Record each result: timestamp, response time (ms), status code, healthy flag, error message | Must |
| HC-6 | Configurable retry count before marking as down (default: 2 consecutive failures) | Must |
| HC-7 | Polly v8 retry and timeout policies for HTTP requests | Must |
| HC-8 | Stagger checks to prevent thundering herd on startup | Should |
| HC-9 | Run checks in parallel with configurable concurrency | Should |

### 4.4 Status Dashboard

| ID | Requirement | Priority |
|----|-------------|----------|
| DASH-1 | Display all monitored endpoints with current status: Up, Down, Degraded, Unknown | Must |
| DASH-2 | Show response time for last successful check | Must |
| DASH-3 | Show uptime percentage for 24h, 7d, and 30d windows | Must |
| DASH-4 | Uptime history timeline (visual bar showing up/down over time) | Must |
| DASH-5 | Show last checked timestamp per endpoint | Must |
| DASH-6 | Auto-refresh via SignalR (Blazor Server real-time updates) | Must |
| DASH-7 | Group endpoints by their Group field with collapsible sections | Should |
| DASH-8 | Response time trend chart per endpoint (ApexCharts) | Should |
| DASH-9 | Overall system status summary at the top of the dashboard | Should |

### 4.5 Site Settings & Branding

| ID | Requirement | Priority |
|----|-------------|----------|
| SS-1 | Configurable site title displayed in header and browser tab | Must |
| SS-2 | Configurable logo URL displayed in the header | Should |
| SS-3 | Configurable accent color applied to the MudBlazor theme | Should |
| SS-4 | Configurable footer text | Should |
| SS-5 | Settings stored in SiteSettings table, seeded with defaults | Must |
| SS-6 | Admin UI for editing site settings at runtime | Must |

### 4.6 Data Management

| ID | Requirement | Priority |
|----|-------------|----------|
| DM-1 | Auto-prune check results older than configurable retention (default 90 days) | Must |
| DM-2 | EF Core migrations for schema management | Must |
| DM-3 | Seed default SiteSettings row on first run | Must |

## 5. Data Model

### SiteSettings
| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| SiteTitle | string | Default: "Status Tracker" |
| LogoUrl | string? | Optional |
| AccentColor | string | Default: MudBlazor primary |
| FooterText | string? | Optional |

### MonitoredEndpoint
| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| Name | string | Display name |
| Group | string? | Optional grouping |
| Url | string | HTTP(S) URL |
| CheckIntervalSeconds | int | Default: 60 |
| ExpectedStatusCode | int | Default: 200 |
| ExpectedBodyMatch | string? | Substring or regex |
| TimeoutSeconds | int | Default: 10 |
| RetryCount | int | Default: 2 |
| IsEnabled | bool | Default: true |
| IsPublic | bool | Default: false (future use) |
| SortOrder | int | Display ordering |
| CreatedAt | DateTime | UTC |
| UpdatedAt | DateTime | UTC |

### CheckResult
| Field | Type | Notes |
|-------|------|-------|
| Id | long | PK |
| EndpointId | int | FK to MonitoredEndpoint |
| Timestamp | DateTime | UTC |
| ResponseTimeMs | int? | Null if connection failed |
| HttpStatusCode | int? | Null if connection failed |
| IsHealthy | bool | Pass/fail flag |
| ErrorMessage | string? | Null on success |

### AppUser
- Extends IdentityUser
- Linked to OAuth external logins via ASP.NET Core Identity

## 6. Non-Functional Requirements

| ID | Requirement | Target |
|----|-------------|--------|
| NFR-1 | Deployment via Docker Compose (app + PostgreSQL + Seq) | Single command |
| NFR-2 | All configuration via environment variables or appsettings | Zero code changes |
| NFR-3 | Health checks run in parallel, staggered on startup | No thundering herd |
| NFR-4 | Structured logging via Serilog | Seq (dev), Elastic APM (prod) |
| NFR-5 | Data retention pruning runs on schedule | Default 90-day retention |
| NFR-6 | Dashboard loads in under 2 seconds with 50 endpoints | Responsive UI |

## 7. Technical Architecture

```
[Browser] <--SignalR--> [Blazor Server (.NET 9)]
                              |
                        [EF Core] --> [PostgreSQL]
                              |
                   [BackgroundService] --> [HTTP endpoints]
                              |
                        [Polly policies]
                              |
                        [Serilog] --> [Seq / Elastic APM]
```

## 8. Release Criteria (v1)

- [ ] OAuth login with at least one provider functional
- [ ] Endpoint CRUD via admin UI
- [ ] Health check engine running checks at configured intervals
- [ ] Dashboard displaying real-time status, uptime %, and history
- [ ] Site settings configurable via admin UI
- [ ] Docker Compose deployment working end-to-end
- [ ] Auto-prune of old check results
- [ ] No hardcoded branding or project-specific values in source
