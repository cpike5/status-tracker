# User Stories

## Status Tracker

**Version:** 1.0
**Date:** 2026-04-11
**Status:** Draft

---

## Epic 1: Authentication & Access Control

### US-1.1: OAuth Login
**As an** admin user
**I want to** log in using my Google, Microsoft, or GitHub account
**So that** I can access the admin features without creating a separate account

**Acceptance Criteria:**
- Login page displays buttons for all configured OAuth providers
- Clicking a provider button redirects to the provider's OAuth flow
- After successful OAuth, the user is redirected to the dashboard
- Only providers with configured client ID/secret env vars are shown
- Users not on the email whitelist are denied access with a clear message

### US-1.2: Email Whitelist Access Control
**As an** instance owner
**I want to** restrict application access to a list of approved email addresses
**So that** only authorized people can view and manage my status page

**Acceptance Criteria:**
- Allowed emails are configurable via environment variable or database
- Login attempt from a non-whitelisted email is rejected
- Rejection displays a user-friendly error (not a stack trace)
- Whitelist changes take effect without application restart (if DB-driven)

### US-1.3: Session Management
**As an** authenticated user
**I want to** remain logged in across browser sessions
**So that** I don't have to re-authenticate every time I visit the dashboard

**Acceptance Criteria:**
- Authentication cookie persists across browser restarts
- Session expires after a configurable inactivity period
- User can explicitly log out

---

## Epic 2: Endpoint Management

### US-2.1: Add Monitored Endpoint
**As an** admin user
**I want to** add a new HTTP endpoint to monitor
**So that** the health check engine starts tracking its availability

**Acceptance Criteria:**
- Form includes fields: Name, URL, Group (optional), Check Interval, Expected Status Code, Expected Body Match (optional), Timeout, Retry Count, Sort Order, Enabled toggle
- URL is validated as a well-formed HTTP(S) URL
- Check Interval, Timeout, and Retry Count have sensible defaults (60s, 10s, 2)
- On save, the endpoint appears in the dashboard within one check interval
- Validation errors display inline next to the relevant field

### US-2.2: Edit Monitored Endpoint
**As an** admin user
**I want to** modify the configuration of an existing endpoint
**So that** I can adjust monitoring parameters without deleting and re-creating

**Acceptance Criteria:**
- All endpoint fields are editable
- Changes take effect on the next health check cycle
- The edit form pre-populates with current values
- Validation rules are the same as the create form

### US-2.3: Delete Monitored Endpoint
**As an** admin user
**I want to** remove an endpoint from monitoring
**So that** decommissioned services no longer appear on the dashboard

**Acceptance Criteria:**
- A confirmation dialog appears before deletion
- Deleting an endpoint removes it from the dashboard
- Associated check results are also deleted (or orphaned gracefully)
- Deletion is immediate; no "soft delete" in v1

### US-2.4: Enable/Disable Endpoint
**As an** admin user
**I want to** temporarily disable monitoring for an endpoint
**So that** planned maintenance doesn't generate false alerts on the dashboard

**Acceptance Criteria:**
- Toggle is accessible from the endpoint list without opening the edit form
- Disabled endpoints stop being polled by the health check engine
- Disabled endpoints appear on the dashboard with a "Paused" or "Disabled" indicator
- Re-enabling resumes checks on the next cycle

### US-2.5: Endpoint List View
**As an** admin user
**I want to** see a list of all configured endpoints with their key settings
**So that** I can quickly audit what is being monitored

**Acceptance Criteria:**
- Table/list shows: Name, URL, Group, Interval, Enabled status, last check status
- List is sortable by Name, Group, and Sort Order
- Each row has Edit and Delete actions

---

## Epic 3: Health Check Engine

### US-3.1: Scheduled Health Checks
**As the** system
**I want to** poll each enabled endpoint at its configured interval
**So that** the dashboard reflects current endpoint health

**Acceptance Criteria:**
- BackgroundService runs continuously while the application is running
- Each endpoint is checked independently on its own interval
- Checks are staggered on startup to avoid thundering herd
- Check results are persisted to the database

### US-3.2: Health Check Evaluation
**As the** system
**I want to** evaluate each health check against the endpoint's expected criteria
**So that** the pass/fail status is accurate

**Acceptance Criteria:**
- Response status code is compared to ExpectedStatusCode
- If ExpectedBodyMatch is configured, response body is checked for the match
- Timeout and connection failures are recorded as unhealthy
- Each result records: timestamp, response time, status code, healthy flag, error message

### US-3.3: Retry Before Failure
**As the** system
**I want to** retry failed checks before marking an endpoint as down
**So that** transient network issues don't cause false negatives

**Acceptance Criteria:**
- Polly retry policy respects the endpoint's RetryCount setting
- Only after all retries fail is the endpoint marked unhealthy
- Retry attempts are logged but only the final result is persisted as a CheckResult
- Timeout policy is applied per-attempt

---

## Epic 4: Status Dashboard

### US-4.1: Current Status Overview
**As an** admin user
**I want to** see the current health status of all monitored endpoints at a glance
**So that** I can quickly identify any services that are down

**Acceptance Criteria:**
- Each endpoint displays: Name, Group, current status (Up/Down/Degraded/Unknown), last response time
- Status is color-coded (green = Up, red = Down, yellow = Degraded, gray = Unknown)
- Endpoints are ordered by Sort Order, then by Name
- Unknown status is shown for endpoints that have never been checked

### US-4.2: Uptime Percentage
**As an** admin user
**I want to** see uptime percentages for 24h, 7d, and 30d windows
**So that** I can assess the reliability of each endpoint over time

**Acceptance Criteria:**
- Uptime % is calculated as (healthy checks / total checks) * 100 for the given window
- All three time windows (24h, 7d, 30d) are displayed per endpoint
- Percentage is formatted to two decimal places (e.g., 99.95%)
- If insufficient data exists for a window, display "N/A" or the available range

### US-4.3: Uptime History Timeline
**As an** admin user
**I want to** see a visual timeline showing up/down history for each endpoint
**So that** I can identify patterns and recurring outages

**Acceptance Criteria:**
- Horizontal bar visualization with colored segments (green/red/yellow)
- Timeline covers at least 30 days of history
- Hovering/clicking a segment shows the time range and status details
- Similar in style to GitHub/Atlassian status page timelines

### US-4.4: Real-Time Updates
**As an** admin user
**I want** the dashboard to update automatically when new health check results arrive
**So that** I always see current data without refreshing the page

**Acceptance Criteria:**
- Dashboard updates are pushed via SignalR (Blazor Server)
- No manual page refresh is required to see new check results
- Status changes (e.g., Up to Down) are reflected within seconds of the check completing
- Connection loss shows a reconnecting indicator

### US-4.5: Endpoint Grouping
**As an** admin user
**I want** endpoints grouped by their Group field on the dashboard
**So that** I can organize services logically (e.g., Production, Staging, Internal)

**Acceptance Criteria:**
- Endpoints with the same Group value are displayed together under a group heading
- Groups are collapsible
- Ungrouped endpoints appear in a default section
- Group ordering is determined by the lowest Sort Order of its members

### US-4.6: Response Time Chart
**As an** admin user
**I want to** see a response time trend chart for each endpoint
**So that** I can identify performance degradation before it becomes an outage

**Acceptance Criteria:**
- Line chart showing response time over time (ApexCharts)
- Selectable time range (1h, 6h, 24h, 7d)
- Chart is accessible from the endpoint detail or dashboard drill-down
- Timeout/failure data points are visually distinct

---

## Epic 5: Site Settings & Branding

### US-5.1: Configure Site Branding
**As an** admin user
**I want to** customize the site title, logo, accent color, and footer text
**So that** the status page matches my organization's branding

**Acceptance Criteria:**
- Admin settings page with fields: Site Title, Logo URL, Accent Color (color picker), Footer Text
- Changes are saved to the database and take effect immediately
- Site title appears in the page header and browser tab title
- Logo URL is rendered as an image in the header
- Accent color is applied to the MudBlazor theme
- Default values are seeded on first run

### US-5.2: Default Branding on First Run
**As an** instance owner
**I want** sensible default branding applied when I first deploy the application
**So that** the status page is usable out of the box before I customize it

**Acceptance Criteria:**
- SiteSettings table is seeded with one row on first run
- Default site title is "Status Tracker" (or similar generic title)
- Default accent color matches the MudBlazor default theme
- Application is fully functional with default settings

---

## Epic 6: Data Management

### US-6.1: Automatic Data Pruning
**As the** system
**I want to** automatically delete health check results older than the retention period
**So that** the database doesn't grow unbounded

**Acceptance Criteria:**
- A background job runs on a schedule (e.g., daily) to prune old records
- Retention period is configurable (default: 90 days)
- Pruning does not lock the database or degrade dashboard performance
- Pruned records are permanently deleted (no archive)

### US-6.2: Database Migrations
**As an** instance owner
**I want** the database schema to be managed via EF Core migrations
**So that** schema updates are applied automatically on deployment

**Acceptance Criteria:**
- Migrations run automatically on application startup
- Migration failures prevent the application from starting (fail fast)
- Migration history is tracked in the standard EF Core migrations table

---

## Epic 7: Deployment

### US-7.1: Docker Compose Deployment
**As an** instance owner
**I want to** deploy the entire application stack with `docker compose up`
**So that** I can get a working status page with minimal effort

**Acceptance Criteria:**
- `docker-compose.yml` includes: application, PostgreSQL, Seq
- All configuration is provided via environment variables
- Application starts, runs migrations, seeds defaults, and begins health checks
- No source code modification is required

### US-7.2: Environment Variable Configuration
**As an** instance owner
**I want** all runtime configuration driven by environment variables
**So that** I can deploy and customize without touching source code

**Acceptance Criteria:**
- Connection string, OAuth credentials, allowed emails, and app settings are all configurable via env vars
- Missing required env vars produce a clear startup error
- Optional env vars have documented defaults
- `appsettings.json` overrides are supported for local development

---

## Story Map Summary

| Epic | Must (v1) | Should (v1) |
|------|-----------|-------------|
| Authentication | US-1.1, US-1.2 | US-1.3 |
| Endpoint Management | US-2.1, US-2.2, US-2.3, US-2.4, US-2.5 | |
| Health Check Engine | US-3.1, US-3.2, US-3.3 | |
| Status Dashboard | US-4.1, US-4.2, US-4.3, US-4.4 | US-4.5, US-4.6 |
| Site Settings | US-5.1, US-5.2 | |
| Data Management | US-6.1, US-6.2 | |
| Deployment | US-7.1, US-7.2 | |

**Total:** 21 user stories across 7 epics
