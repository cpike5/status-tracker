# Health Check Integration Guide

## For Developers and Agents Integrating with Status Tracker

**Version:** 1.0  
**Date:** 2026-04-18  
**Status:** Draft

---

## 1. Overview

Status Tracker is a self-hosted HTTP monitoring application that periodically polls registered endpoints and records whether they responded successfully. It presents results on a real-time dashboard, showing current status (Up / Down / Degraded / Unknown), response time trends, and uptime percentages over 24-hour, 7-day, and 30-day rolling windows.

This guide is addressed to the developer or agent responsible for a web application that has been (or will be) registered as a monitored endpoint in Status Tracker. Its purpose is to specify exactly what your application must expose so that the monitoring system can correctly determine its health. Non-compliance does not break Status Tracker — it just means the data on the dashboard will be wrong.

---

## 2. How Monitoring Works

Understanding what Status Tracker does on its end allows you to reason about what your endpoint will receive and what constitutes a passing result.

### 2.1 Request Mechanics

- Status Tracker sends an **HTTP GET** request to the configured URL.
- No authentication headers are added. Requests originate from the Status Tracker server's IP address.
- A `User-Agent` header is present (standard .NET `HttpClient` default).
- No request body is sent.

### 2.2 Pass/Fail Determination

A check result is marked **healthy** when all of the following are true:

1. The HTTP response is received before the configured timeout elapses.
2. The response status code matches the configured expected status code (default: `200`).
3. If an expected body match string is configured: the response body contains the expected substring OR matches the expected regex pattern.

A check result is marked **unhealthy** when any of the following occur:

| Failure condition | Example |
|-------------------|---------|
| Request timeout | No response within `TimeoutSeconds` |
| Connection refused | App is down or port is wrong |
| DNS resolution failure | Hostname not resolvable |
| Wrong status code | App returns `302` but expected code is `200` |
| Body match failure | Body match is configured and the response body does not contain the expected substring or does not match the expected regex |

### 2.3 Retry Behavior

Status Tracker uses **Polly v8** resilience pipelines. Before recording an unhealthy result, it retries the request up to `RetryCount` times (default: 2). Only after all retries are exhausted does it write a failing `CheckResult` to the database.

With default settings (2 retries, 10-second timeout), the worst-case elapsed time before a failure is confirmed is approximately 30 seconds. This means transient blips — a single slow response or one dropped connection — do not necessarily appear on the dashboard.

### 2.4 What Gets Recorded

Each check stores a `CheckResult` row in the database containing:

| Field | Description |
|-------|-------------|
| `Timestamp` | UTC time the check completed |
| `ResponseTimeMs` | Total time from request dispatch to response received (null on connection failure) |
| `HttpStatusCode` | HTTP status code returned (null on connection failure) |
| `IsHealthy` | Boolean pass/fail determination |
| `ErrorMessage` | Human-readable failure reason (null on success) |

### 2.5 Check Interval

Checks run at the per-endpoint `CheckIntervalSeconds` value stored in the database (default: 60 seconds). The check interval is wall-clock time between the end of the previous check and the start of the next. Checks across different endpoints run in parallel, up to the configured `MaxConcurrency` limit (default: 10).

---

## 3. Health Check Endpoint Requirements

The following requirements apply to any endpoint registered with Status Tracker.

### Must-haves

**Respond to HTTP GET.** Status Tracker sends only GET requests. Endpoints that reject GET (e.g., return `405 Method Not Allowed`) will be recorded as unhealthy.

**Return the expected HTTP status code.** The default expected code is `200 OK`. If your endpoint returns a different 2xx code (e.g., `204 No Content`), the expected code must be changed to match in the endpoint configuration. The check is an exact match — `200` does not match `201`.

**Respond within the timeout.** The default timeout is 10 seconds. Your endpoint must produce a complete response (headers and body) before this deadline. Recommend targeting well under 2 seconds; see section 8 for guidance on deep vs. shallow checks.

**Be reachable from the Status Tracker server.** The server makes outbound HTTP(S) requests directly to the configured URL. Firewalls, VPNs, or network policies must permit inbound connections from the Status Tracker host.

### Must-nots

**Do not require authentication.** Status Tracker sends no credentials. Any endpoint that returns `401 Unauthorized` or `403 Forbidden` will be recorded as unhealthy on every check. If your application places authentication middleware in front of all routes, you must either exclude the health check path from that middleware or configure Status Tracker to expect a `401` status code (which only confirms reachability, not actual health).

**Do not perform expensive or side-effecting operations.** Health checks fire frequently (every 30–60 seconds is typical). Do not trigger emails, queue jobs, increment counters, or run full database migrations in response to a health check request. The endpoint must be safe to call idempotently at high frequency.

**Do not redirect to a login page.** If your app redirects unauthenticated GET requests to `/login` (returning `302 Found`), Status Tracker will record the check as unhealthy because the status code does not match the expected `200`. Explicitly exclude the health check path from redirect middleware.

### Recommendations

- Place the endpoint at a conventional path such as `/health`, `/healthz`, or `/status`.
- Keep the endpoint lightweight. It should complete in under 500 milliseconds under normal conditions.
- Check downstream dependencies (database connectivity, cache availability) in the health check response if you want the endpoint to reflect real application health, not just "process is alive." See section 8 for trade-offs.
- Ensure the path does not expire or rotate (do not put it behind feature flags or A/B test routing).

---

## 4. Recommended Response Format

Status Tracker does not require a specific response body format. It reads the body only when an `ExpectedBodyMatch` value is configured for the endpoint; otherwise the body is ignored.

That said, returning structured JSON is strongly recommended because it makes the endpoint useful for other tooling (load balancers, uptime robots, manual inspection) and enables body-match rules in Status Tracker.

### Minimal response

```json
{
  "status": "healthy",
  "timestamp": "2026-04-18T14:00:00Z"
}
```

### Response with dependency details

```json
{
  "status": "healthy",
  "timestamp": "2026-04-18T14:00:00Z",
  "version": "1.4.2",
  "checks": {
    "database": "healthy",
    "cache": "healthy",
    "externalApi": "healthy"
  }
}
```

### Degraded response (still 200, but signals partial failure)

```json
{
  "status": "degraded",
  "timestamp": "2026-04-18T14:00:00Z",
  "checks": {
    "database": "healthy",
    "cache": "unhealthy"
  }
}
```

**Status Tracker body matching example:** If you return `"status": "healthy"` in the body, you can configure the endpoint in Status Tracker with `ExpectedBodyMatch = "\"status\": \"healthy\""`. Status Tracker will then fail the check any time the body does not include that substring — useful for catching cases where the app returns `200` but reports internal degradation.

> Note: the `status` field values (`"healthy"`, `"degraded"`, `"unhealthy"`) shown above are conventions used in this guide. ASP.NET Core's built-in health checks middleware uses `"Healthy"`, `"Degraded"`, and `"Unhealthy"` (capitalized). Either convention works — just be consistent, because body-match rules are case-sensitive substring or regex matches.

---

## 5. Implementation Examples

### 5.1 ASP.NET Core (built-in health checks middleware)

The simplest approach uses Microsoft's built-in `Microsoft.Extensions.Diagnostics.HealthChecks` package, which ships with .NET and requires no additional NuGet packages for a basic setup.

```csharp
// Program.cs

// Register the health check service
builder.Services.AddHealthChecks()
    .AddNpgsql(connectionString, name: "database") // requires AspNetCore.HealthChecks.NpgsQL
    .AddCheck("self", () => HealthCheckResult.Healthy());

// Map the /health endpoint — no auth, no HTTPS redirection, no rate limiting
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    // UIResponseWriter is from AspNetCore.HealthChecks.UI.Client
    // Remove it and use the default if you do not want that dependency
});
```

To exclude the health endpoint from authentication middleware:

```csharp
app.MapHealthChecks("/health").AllowAnonymous();
```

Default response (without `UIResponseWriter`) is plain text: `Healthy`, `Degraded`, or `Unhealthy` with HTTP status `200`, `200`, or `503` respectively.

To always return `200` regardless of check outcome (so Status Tracker sees `200` as the expected code):

```csharp
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResultStatusCodes =
    {
        [HealthStatus.Healthy]   = StatusCodes.Status200OK,
        [HealthStatus.Degraded]  = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status200OK
    }
});
```

Alternatively, configure Status Tracker to expect `503` for unhealthy states so the status code itself signals failure.

---

### 5.2 Express.js / Node.js

```javascript
// health.js route

const router = require('express').Router();

router.get('/health', async (req, res) => {
  const timestamp = new Date().toISOString();

  try {
    // Optional: check DB connectivity
    await db.raw('SELECT 1');

    res.status(200).json({
      status: 'healthy',
      timestamp,
    });
  } catch (err) {
    res.status(503).json({
      status: 'unhealthy',
      timestamp,
      error: err.message,
    });
  }
});

module.exports = router;
```

Register the route before authentication middleware so that it does not require a session:

```javascript
// app.js
app.use('/health', require('./routes/health')); // before passport/session middleware
app.use(require('./middleware/auth'));
```

---

### 5.3 Python — FastAPI

```python
from fastapi import FastAPI
from datetime import datetime, timezone

app = FastAPI()

@app.get("/health")
async def health_check():
    return {
        "status": "healthy",
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }
```

To include a database check:

```python
from sqlalchemy.ext.asyncio import AsyncSession
from fastapi import Depends

@app.get("/health")
async def health_check(db: AsyncSession = Depends(get_db)):
    try:
        await db.execute(text("SELECT 1"))
        db_status = "healthy"
    except Exception:
        db_status = "unhealthy"

    overall = "healthy" if db_status == "healthy" else "degraded"

    return {
        "status": overall,
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "checks": {"database": db_status},
    }
```

FastAPI does not add authentication by default, so `/health` is publicly accessible unless you explicitly add a dependency. If you use OAuth2 or API key dependencies globally, exclude the health route:

```python
# Do not add the auth dependency to the health route function
@app.get("/health")  # No `dependencies=[Depends(verify_token)]`
async def health_check():
    ...
```

---

### 5.4 Python — Flask

```python
from flask import Flask, jsonify
from datetime import datetime, timezone

app = Flask(__name__)

@app.route("/health")
def health_check():
    return jsonify({
        "status": "healthy",
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }), 200
```

If Flask-Login or a `@login_required` decorator is applied globally via a `before_request` handler, whitelist the health route:

```python
@app.before_request
def require_login():
    if request.endpoint == 'health_check':
        return  # skip auth for health check
    if not current_user.is_authenticated:
        return redirect(url_for('login'))
```

---

### 5.5 Static / Nginx

If the application is a static site or you simply need nginx to respond with `200` at a specific path:

```nginx
location /health {
    access_log off;
    return 200 '{"status":"healthy"}';
    add_header Content-Type application/json;
}
```

This is a shallow check only — it confirms nginx is serving but nothing about the upstream application.

---

## 6. Checklist for Existing Applications

Run through this checklist before registering an endpoint in Status Tracker, or when diagnosing a false negative.

- [ ] **GET /health returns 200.** Verify with `curl -i https://your-app.example.com/health`. The first line of output should be `HTTP/1.1 200 OK` (or `HTTP/2 200`).
- [ ] **Responds in under 2 seconds.** Run `curl -o /dev/null -s -w "%{time_total}\n" https://your-app.example.com/health`. The reported time should be well below `2.000`.
- [ ] **Endpoint is unauthenticated.** Run the `curl` command above without any cookies, tokens, or credentials. You should still get `200`, not `302`, `401`, or `403`.
- [ ] **No side effects.** Review what the endpoint does. It should read state, not mutate it. No emails sent, no jobs queued, no audit log entries written.
- [ ] **Reachable from the Status Tracker host.** If Status Tracker and your app are on different networks, ensure the health check URL is accessible from the Status Tracker server's IP. Test from that host if possible.
- [ ] **Does not redirect.** Check that the response is not a `301`, `302`, or `307`. Redirects are not followed as a successful health check response; the redirect status code itself is evaluated against the expected code.
- [ ] **Body is stable if body matching is configured.** If you have set `ExpectedBodyMatch` in Status Tracker, verify that the response body reliably contains that substring. Watch for changes to the JSON schema or field names that would silently break the match.
- [ ] **Dependency checks are appropriate.** Decide whether the endpoint should check downstream systems (database, cache, external APIs). If it does, ensure those checks have their own short timeouts so a slow dependency does not cause the health endpoint to time out from Status Tracker's perspective.

---

## 7. Common Pitfalls

### Health endpoint behind authentication middleware

**Symptom:** Status Tracker records `401`, `403`, or `302` (redirect to login) on every check.

**Fix:** Explicitly exclude the health check path from authentication middleware. In ASP.NET Core: `.AllowAnonymous()` on the mapped endpoint. In Express: register the route before `passport.authenticate()`. In Flask: skip `@login_required` in `before_request`.

---

### Endpoint does too much work and times out

**Symptom:** Status Tracker records timeouts or very high response times. The endpoint is healthy from a browser but fails the automated check.

**Fix:** Reduce what the health endpoint does. Heavy queries, synchronous external API calls, and large response bodies all add latency. Target under 500 ms. If you need deep dependency checks, set short per-check timeouts inside the health handler.

---

### Returning the wrong status code

**Symptom:** The app is running fine but Status Tracker shows it as unhealthy.

**Fix:** The expected status code in Status Tracker must exactly match what the endpoint returns. Common mismatches:
- ASP.NET Core's health checks middleware returns `503` for unhealthy results by default. If Status Tracker expects `200`, unhealthy results will fail the code check.
- Some frameworks return `204 No Content` for empty responses. Configure Status Tracker to expect `204` or change the endpoint to return `200` with a body.
- Load balancers sometimes front the app and return their own status codes on certain paths.

---

### Response body changes break body match rules

**Symptom:** Status Tracker starts recording failures after a deployment even though the app is healthy.

**Fix:** If `ExpectedBodyMatch` is configured, the body must reliably contain that substring on every successful response. Version bumps, schema changes, or whitespace differences can silently break substring or regex matches. When updating your health endpoint response format, update the corresponding `ExpectedBodyMatch` in Status Tracker at the same time.

---

### Firewall or network policy blocks the Status Tracker server

**Symptom:** Status Tracker records connection timeouts or DNS failures, but the endpoint is accessible from other machines.

**Fix:** Identify the outbound IP address of the Status Tracker server and add it to the firewall allowlist for your application's host and port. If Status Tracker is inside a private network and your app is external, ensure the relevant network routing exists.

---

### HTTPS certificate errors

**Symptom:** Status Tracker records TLS/SSL errors for HTTPS endpoints.

**Fix:** The certificate on your endpoint must be valid (not expired, not self-signed unless explicitly trusted by the Status Tracker host). Self-signed certificates will cause `HttpClient` to fail with a certificate validation error, which is treated as a connection failure. Options: use a valid certificate (Let's Encrypt is free), or configure the Status Tracker deployment to trust your CA. The latter requires application-level changes and is not a configuration option.

---

## 8. Advanced Patterns

### Shallow vs. deep health checks

A **shallow check** confirms only that the process is running and the web server is accepting connections:

```json
{ "status": "healthy" }
```

A **deep check** verifies that key downstream dependencies are reachable and functional:

```json
{
  "status": "healthy",
  "checks": {
    "database": "healthy",
    "redis": "healthy",
    "paymentApi": "healthy"
  }
}
```

Trade-offs:

| | Shallow | Deep |
|--|---------|------|
| Speed | Fast (sub-millisecond) | Slower (depends on dependencies) |
| Risk of false positives | Higher (app up but DB down) | Lower |
| Risk of timeout | Very low | Higher if dependencies are slow |
| Complexity | None | Requires per-dependency checks with timeouts |

Recommendation: use a shallow check as the primary Status Tracker endpoint to minimize false negatives from slow dependencies. Add a separate deep-check endpoint (e.g., `/health/deep`) for human inspection or a secondary monitor with a longer timeout.

---

### Dependency health checks

When a deep check includes downstream dependencies, each dependency check should have an explicit short timeout — typically 1–2 seconds — so a slow dependency does not blow the entire health endpoint's response time past Status Tracker's timeout.

In ASP.NET Core:

```csharp
builder.Services.AddHealthChecks()
    .AddNpgsql(
        connectionString,
        name: "database",
        timeout: TimeSpan.FromSeconds(2))
    .AddRedis(
        redisConnectionString,
        name: "cache",
        timeout: TimeSpan.FromSeconds(1));
```

---

### Degraded state reporting

ASP.NET Core's `HealthStatus.Degraded` maps to HTTP `200` by default (you can override this). It represents a state where the application is functioning but not at full capacity — for example, a replica database is unavailable but the primary is healthy.

If you want Status Tracker to surface degraded state as a distinct signal, configure `ExpectedBodyMatch` to match `"Healthy"` (capitalized, the ASP.NET Core default). A degraded response body will contain `"Degraded"` instead, causing the body match to fail and the endpoint to appear unhealthy on the dashboard.

Alternatively, configure Status Tracker to expect `200` and treat all 200 responses as healthy. Use the body match only when you want strict differentiation.

---

### Multiple endpoints for different subsystems

Rather than one catch-all health endpoint, register multiple endpoints in Status Tracker for different subsystems:

| Endpoint | Checks | Purpose |
|----------|--------|---------|
| `https://app.example.com/health` | Process alive, DB connected | Primary operational health |
| `https://app.example.com/health/queue` | Message queue consumer lag | Background worker health |
| `https://api.example.com/health` | API surface, auth service | Public API health |
| `https://cdn.example.com/health.json` | Static file delivery | CDN / asset pipeline health |

This approach gives the Status Tracker dashboard granular visibility into which subsystem is failing rather than showing the entire application as down when only one component is degraded.

---

## 9. Configuring Status Tracker Per Endpoint

When registering an endpoint in Status Tracker's admin UI, the following per-endpoint settings control exactly how the health check behaves. Understanding these settings helps you align your endpoint implementation with the monitoring configuration.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| **Name** | String | — | Display name on the dashboard (e.g., "Payment API") |
| **Group** | String | (none) | Optional grouping label (e.g., "Production", "Staging") |
| **URL** | String | — | The full HTTP(S) URL to send GET requests to |
| **Check Interval** | Integer (seconds) | 60 | How often to poll. Minimum is determined by the engine's tick resolution. |
| **Expected Status Code** | Integer | 200 | The exact HTTP status code that constitutes a passing result |
| **Expected Body Match** | String | (none) | Optional substring or regex pattern that must appear in the response body. Leave empty to skip body matching. |
| **Timeout** | Integer (seconds) | 10 | Per-request timeout. Requests that do not complete within this window are counted as failures. |
| **Retry Count** | Integer | 2 | Number of Polly retry attempts before writing a failing result. Higher values reduce false negatives at the cost of slower failure detection. |
| **Enabled** | Boolean | true | When disabled, the endpoint is excluded from the health check scheduler. No new results are recorded. |
| **Sort Order** | Integer | — | Controls display position on the dashboard. Lower numbers appear first. |

### Global defaults

If per-endpoint values are not set, the engine falls back to application-level defaults configured via environment variables:

| Environment variable | Default | Per-endpoint override |
|---------------------|---------|----------------------|
| `HealthCheck__DefaultIntervalSeconds` | `60` | `CheckIntervalSeconds` |
| `HealthCheck__DefaultTimeoutSeconds` | `10` | `TimeoutSeconds` |
| `HealthCheck__DefaultRetryCount` | `2` | `RetryCount` |

See `docs/configuration.md` for the full environment variable reference.

---

## Related Documentation

| Document | Path | Description |
|----------|------|-------------|
| Configuration Reference | `docs/configuration.md` | Full environment variable reference, global health check defaults |
| Architecture | `docs/architecture.md` | Health check engine design, data model, Polly policy details |
| Requirements | `docs/requirements.md` | Feature spec including health check pass/fail logic |
