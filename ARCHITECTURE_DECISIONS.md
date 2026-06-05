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
