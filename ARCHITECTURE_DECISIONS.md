# Architecture Decisions

## Service Lifetime Choices

### RequestContext → Scoped
Holds per-request data: CorrelationId, UserAgent, StartTimeUtc.
Must be isolated per request — a Singleton would cause state bleeding across concurrent users.
A Transient would break correlation tracking (different instances within the same request).

### OrderProcessor → Scoped
Depends on RequestContext (Scoped). Injecting a Scoped service into a Singleton causes a
Captive Dependency — the short-lived dependency gets trapped and never released.
"One processor per request" aligns exactly with Scoped semantics.

### StatisticsCollector → Singleton
Accumulates application-wide metrics across all requests and the full application lifetime.
Scoped would reset counters on every request. Must be Singleton.
Requires thread-safe access — multiple concurrent requests write to shared state simultaneously.

### OrderMetrics → Singleton
Owns application-wide `Counter` and `Histogram` instruments that are thread-safe and should be
created once rather than once per request. It contains no request-specific state.

### RandomGenerator → Singleton
Stateless — no fields, no mutable state. No reason to create a new instance per request.
Uses Random.Shared (thread-safe since .NET 6) instead of new Random() which is not thread-safe —
sharing a single Random instance across threads without synchronization produces undefined behavior.

---

## RequestContext Design

### Immutability
All properties are set once in the constructor and never change.
CorrelationId must remain stable for the entire request lifetime — if it changed mid-request,
all logs and traces for that request would become untraceable.

### CorrelationId → Guid
The server generates the CorrelationId autonomously — no coordination with any other system needed.
Guid is 128-bit, globally unique in practice, generated without shared state.
An int would require a counter (shared mutable state) or an external sequence.
UUID and Guid are the same standard — UUID is the RFC name, Guid is the .NET/Microsoft name.

### UserAgent from IHttpContextAccessor
RequestContext is constructed by the DI container, not by the endpoint handler.
IHttpContextAccessor is the only way for a DI-managed service to access the current
HttpContext without being directly in the request pipeline.

---

## Configuration — IOptions\<OrderProcessingOptions\>

MinDelayMs and MaxDelayMs are managed via IOptions<T> with ValidateOnStart rather than
reading directly from IConfiguration because:

- **ValidateOnStart**: validation rules run at startup, before any request is served.
  A misconfigured delay range causes a hard failure immediately, not silently at runtime.
- **Environment flexibility**: operations teams can tune delay values per environment
  (dev/staging/production) without touching code.
- **Type safety**: strongly-typed options class vs string-based IConfiguration keys.

---

## async/await and CancellationToken

Task.Delay is used instead of Thread.Sleep to simulate processing time.
Thread.Sleep blocks the thread — under concurrent load this exhausts the thread pool.
Task.Delay releases the thread back to the pool while waiting.

CancellationToken is propagated from the HTTP pipeline into ProcessAsync and Task.Delay.
If the client disconnects mid-request, the delay is cancelled immediately,
avoiding wasted work on a response nobody will receive.

---

## Thread Safety — StatisticsCollector

Two concurrent requests writing to the same Singleton creates a race condition:

```
Thread A reads: total = 5
Thread B reads: total = 5   ← before A wrote
Thread A writes: total = 6
Thread B writes: total = 6  ← overwrites A, should be 7
```

**Decision:** Single lock covering both `_totalOrdersProcessed` and `_totalProcessingDurationMs`.
Updating them under separate locks would break average calculation (TOCTOU):
a reader could see orders=6 with duration from only 5 updates.

`Queue<long>` for last-five durations is also covered by the same lock to keep the
snapshot consistent with the counters.

**Why not Interlocked?** Interlocked operates on a single field atomically.
We need two fields updated together — only lock guarantees that.

**Lock contention under load:**
The lock body contains only field increments and a queue enqueue — a very short critical section.
Under high concurrency, threads compete for the lock but the wait time is expected to be negligible.
To verify this in production, tools like `dotnet-counters` or `dotnet-trace` can measure
actual lock contention and confirm it is not a bottleneck.

If the lock body were expensive (e.g. database write, heavy computation, or I/O),
the appropriate solutions would be:
- Extract computation outside the lock, lock only the write
- `ReaderWriterLockSlim` — allows parallel reads, exclusive lock only on writes
- `Channel<T>` — threads enqueue data without locking; a single consumer updates state,
  eliminating contention entirely

---

## Input Validation

orderId validation is extracted to a dedicated `OrderValidator` class rather than living
inline in Program.cs. Program.cs should wire things together, not contain business rules.

Constraints:
- orderId must not be null or whitespace
- orderId must not exceed 256 characters (common HTTP infrastructure limit)
- orderId is a string, not int: the client generates it, supports any format,
  and enables the Idempotency Key Pattern in future.

---

## Intentional Bug Demonstration

RequestContext registered as Singleton (wrong) vs Scoped (correct) is demonstrated
in BugDemoSpecs. This is the most dangerous silent bug in DI:

- This bug bypasses ASP.NET Core's scope validation entirely — a Singleton is technically
  resolvable from anywhere, so no InvalidOperationException is thrown even in Development
- The application starts cleanly but silently shares state across requests
- In Production the behavior is identical — silent and dangerous (ValidateScopes=false by default)
- Shared CorrelationId across requests makes distributed tracing useless
- In a real system with UserId this would be a critical security vulnerability

---

## Security Headers

Two security response headers are added via middleware on every request:

- **X-Content-Type-Options: nosniff** — prevents browsers from guessing the content type.
  Without it, a browser might interpret a JSON response as JavaScript and execute it.
- **X-Frame-Options: DENY** — prevents the API from being embedded inside an `<iframe>`.
  Protects against Clickjacking attacks where a user unknowingly interacts with a hidden frame.

The Server response header is suppressed via Kestrel configuration (`AddServerHeader = false`)
to avoid exposing server implementation details to potential attackers.

---

## Observability — Request Correlation and Structured Logging

Every response includes an **X-Correlation-ID** header containing the server-generated
CorrelationId. This lets a caller report the identifier and lets an operator find the
corresponding server-side logs. It is an observability header, not a security control.

`OrderProcessor.ProcessAsync` opens an `ILogger.BeginScope` with `CorrelationId` and `OrderId`
as structured properties before doing any work. Every log call inside the processing operation
inherits these properties without repeating them in each message.

The console logger uses the JSON formatter with scopes enabled, so these properties are visible
as structured fields during local execution rather than existing only inside the logging pipeline.

This is a meaningful improvement over embedding `[CorrelationId=...]` manually in each message:
- The properties are searchable as first-class fields in structured log sinks (Seq, Elasticsearch, Loki)
- Adding a new property to the scope automatically applies to all future log lines in that operation
- The log messages themselves stay short and readable

---

## Observability — System.Diagnostics.Metrics

`OrderMetrics` wraps a `Meter` named `"OrderProcessing.Api"` with two instruments:

| Instrument | Name | Type | Tags |
|---|---|---|---|
| Counter | `order_processing.operations` | `Counter<long>` | `outcome`: success / cancelled / failed |
| Histogram | `order_processing.duration` | `Histogram<long>` | `outcome`: success / cancelled / failed |

**Why IMeterFactory, not `new Meter()`?**
`IMeterFactory` (registered via `services.AddMetrics()`) integrates with .NET's DI lifecycle —
the factory manages meter lifetime and isolates meters created by different service providers,
which is useful for tests. A manually created `Meter` would need an explicitly managed lifetime.

**Why only `outcome` tag?**
Tags are the dimensions along which metrics are split. `OrderId` and `CorrelationId` are
high-cardinality values (unique per request) — using them as tags would create millions of
time series, overwhelming any metrics backend. Outcome is a low-cardinality enum: three values max.

**Local observation:**
```
dotnet tool install --global dotnet-counters
dotnet-counters monitor --counters OrderProcessing.Api -- \
  dotnet run --project OrderProcessing.Api/OrderProcessing.Api.csproj
```

**Production path:** OpenTelemetry would be configured to collect the custom meter explicitly:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(OrderMetrics.MeterName)
        .AddPrometheusExporter());
```

The instrumentation in `OrderMetrics` remains backend-agnostic. Prometheus is one possible
metrics backend; an OTLP exporter could send the same measurements to an OTLP-compatible collector.

**Signals and alerts in production:**
- Health endpoint unavailable
- Processing failure ratio above an agreed threshold
- p95 processing duration above the service objective
- HTTP 429 response rate, collected from ASP.NET Core HTTP server metrics
- Runtime CPU, allocation rate, thread-pool queue length, and lock contention

---

## Dependency Security

Top-level test packages were updated to patched versions to resolve transitive vulnerabilities:
- `Microsoft.AspNetCore.Mvc.Testing` → 8.0.15
- `Microsoft.NET.Test.Sdk` → 17.13.0
- `xunit` → 2.9.3
- `xunit.runner.visualstudio` → 3.0.0

Updating the top-level packages is preferred over pinning transitive dependencies directly,
as it keeps the dependency graph clean and maintainable.
A clean scan (`dotnet list package --vulnerable --include-transitive`) is part of a standard
CI pipeline and should pass before any submission or deployment.

---

## Rate Limiting

Implemented with a fixed-window limiter applied **only** to `POST /api/orders/process`.
`GET /api/orders/stats` and `GET /health` are intentionally excluded — they must remain
accessible even when the processing endpoint is under load or rate-limited.

Default: 100 requests per minute, configurable via `appsettings.json`.
The value of 100 is arbitrary for a simulator — in production it would be determined by
expected client traffic and server capacity.

All clients currently share a single bucket. Per-client partitioning (by IP or API key)
would be the next step for production to prevent a single client from exhausting the quota.

---

## Health Check

Implemented at `GET /health` using .NET's built-in `AddHealthChecks()`.
Although there are no external dependencies to probe (no database, no queue),
the endpoint serves as a standard liveness signal for orchestrators (Docker, Kubernetes)
and monitoring tools to verify the process is alive and responsive.

---

## Considered but Not Implemented

**Authentication (JWT / API Key)**
This is a simulator with no real users. Adding authentication would add infrastructure
complexity with no value. In production: API Key for service-to-service, JWT for user-facing.

**OpenTelemetry Exporter**
The metrics layer (`System.Diagnostics.Metrics`) and the CorrelationId propagation are in place.
An OpenTelemetry `MeterProvider` would need to register `OrderMetrics.MeterName` and configure
an exporter. Prometheus or an OTLP-compatible metrics backend could then collect the measurements.
Jaeger and Zipkin are trace backends and would be relevant if distributed tracing were configured.
No exporter is implemented here because the assignment provides no telemetry collector or backend.
