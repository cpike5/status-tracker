# Business Requirements Document (BRD)

## Status Tracker

**Version:** 1.0
**Date:** 2026-04-11
**Status:** Draft

---

## 1. Executive Summary

Status Tracker is a self-hosted status page application that monitors HTTP endpoints and displays their health on a real-time dashboard. It targets developers, DevOps engineers, and small teams who need infrastructure visibility without vendor lock-in or recurring SaaS fees.

## 2. Business Problem

Monitoring the availability of web services, APIs, and websites is a fundamental operational need. Current solutions fall into two categories:

- **Commercial SaaS** (Pingdom, UptimeRobot, StatusPage) — recurring costs, data stored on third-party infrastructure, limited customization, and branding controlled by the vendor.
- **Open-source alternatives** — often complex to deploy, require YAML/JSON configuration files, lack a polished UI, or are abandoned projects with security risks.

Teams and individuals need a **simple, self-hosted, zero-config-file** solution they can deploy with `docker compose up` and manage entirely through a web UI.

## 3. Business Objectives

| # | Objective | Success Metric |
|---|-----------|----------------|
| 1 | Eliminate dependency on third-party uptime monitoring SaaS | Application deployed and monitoring endpoints without external service |
| 2 | Reduce time-to-deploy for new users to under 10 minutes | Clone, configure env vars, `docker compose up`, monitoring active |
| 3 | Provide real-time health visibility for all monitored services | Dashboard reflects endpoint state within one check interval |
| 4 | Enable fully branded status pages without code changes | Site title, logo, colors, and footer configurable at runtime |

## 4. Stakeholders

| Role | Responsibility |
|------|----------------|
| **Instance Owner** | Deploys the application, configures environment variables, manages infrastructure |
| **Authenticated Admin** | Manages monitored endpoints, configures site branding, views health data |
| **Public Viewer** (future) | Views read-only status of endpoints marked as public |

## 5. Scope

### 5.1 In Scope (v1)

- HTTP/HTTPS endpoint health monitoring with configurable intervals
- Real-time status dashboard with uptime history and response time metrics
- Database-driven endpoint and branding configuration (no config files)
- OAuth authentication (Google, Microsoft, GitHub) with email whitelist
- Docker Compose deployment (app + PostgreSQL + Seq)
- Automatic data retention and pruning

### 5.2 Out of Scope (v1)

- Notification channels (email, Slack, Discord, webhooks)
- Non-HTTP check types (TCP, ICMP, custom scripts)
- Multi-user teams and role-based access control
- Incident management and manual status overrides
- SSL certificate expiry monitoring
- Public-facing unauthenticated status page

## 6. Business Rules

| # | Rule |
|---|------|
| BR-1 | All administrative features require OAuth authentication. No anonymous admin access. |
| BR-2 | User access is controlled by an email whitelist. Open registration is not permitted. |
| BR-3 | No project-specific names, URLs, or branding may be hardcoded in source code. The application must be fully generic. |
| BR-4 | All secrets and credentials must be provided via environment variables, never stored in source code or config files committed to version control. |
| BR-5 | Health check results older than the configured retention period (default 90 days) must be automatically pruned. |
| BR-6 | The application must be deployable via a single `docker compose up` command with no source code modifications. |

## 7. Assumptions

- Users have access to a Linux server or VM capable of running Docker.
- Users can configure DNS and, if needed, a reverse proxy for HTTPS termination.
- At least one OAuth provider (Google, Microsoft, or GitHub) will be configured.
- PostgreSQL is the only supported database engine for v1.

## 8. Constraints

- **.NET 9** is the target runtime; the application will not support earlier framework versions.
- **Blazor Server** render mode requires a persistent SignalR connection, limiting horizontal scaling without sticky sessions.
- OAuth providers require registered applications with callback URLs, which the user must configure externally.

## 9. Dependencies

| Dependency | Type | Notes |
|------------|------|-------|
| PostgreSQL | Infrastructure | Primary data store |
| Seq | Infrastructure | Structured log sink (dev environment) |
| Elastic APM | Infrastructure | Application performance monitoring (prod) |
| OAuth Providers | External Service | Google, Microsoft, and/or GitHub for authentication |

## 10. Risks

| # | Risk | Impact | Mitigation |
|---|------|--------|------------|
| R-1 | Blazor Server SignalR connection limits user scale | High concurrency could exhaust server connections | Document connection limits; public status page (future) uses static rendering |
| R-2 | OAuth provider outage prevents admin login | No admin access during outage | Support multiple providers; warn if only one is configured |
| R-3 | Health check thundering herd on startup | Burst of outbound requests when many endpoints are configured | Stagger initial checks; use Polly rate limiting |
| R-4 | Database growth from check results | Storage increases linearly with endpoints and frequency | Auto-prune with configurable retention; document sizing guidance |
