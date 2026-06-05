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

### RandomGenerator → Singleton
Stateless — no fields, no mutable state. No reason to create a new instance per request.
Uses Random.Shared (thread-safe since .NET 6) instead of new Random() which is not thread-safe
and returns 0 under concurrent access.

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

- ASP.NET Core throws InvalidOperationException in Development (ValidateScopes=true)
- In Production it is silent (ValidateScopes=false by default)
- Shared CorrelationId across requests makes distributed tracing useless
- In a real system with UserId this would be a critical security vulnerability

---

## Considered but Not Implemented

**Authentication (JWT / API Key)**
This is a simulator with no real users. Adding authentication would add infrastructure
complexity with no value. In production: API Key for service-to-service, JWT for user-facing.

**Rate Limiting**
.NET 8 has built-in rate limiting middleware (AddRateLimiter / UseRateLimiter).
Not implemented because this is a local simulator, not an internet-facing service.
Worth adding before any public deployment.

**OpenTelemetry / Distributed Tracing**
CorrelationId is propagated through all responses and logs, which is the foundation
for distributed tracing. Full OpenTelemetry instrumentation (traces, metrics, logs)
would be the next step for a production system.

**Health Checks**
No external dependencies (no database, no message queue) — a health check would
always return Healthy and add no diagnostic value here.
