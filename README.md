# Order Processing Simulator (.NET 8)

Minimal API demonstrating DI lifetimes, concurrency, and state boundaries.

## Endpoints

- `POST /api/orders/process` — simulate order processing, returns CorrelationId and duration
- `GET /api/orders/stats` — returns total orders, average duration, last 5 durations

## Project Structure

```
OrderProcessing.Api/
  Contracts/          # Request/response records and configuration options
  DependencyInjection/# Service registration
  Services/           # IRequestContext, IOrderProcessor, IStatisticsCollector, IRandomGenerator
  Validation/         # OrderValidator
  Program.cs          # Wiring: middleware, DI, endpoints

OrderProcessing.Api.Tests/
  OrderApiSpecs.cs        # Integration tests — HTTP end-to-end
  ServiceLifetimeSpecs.cs # DI lifetime verification
  BugDemoSpecs.cs         # Intentional bug demonstration and fix
  StatsEdgeCaseSpecs.cs   # Input validation and zero-stats baseline
```

## Runtime Flow

```
POST /api/orders/process
  → OrderValidator         (validate orderId)
  → OrderProcessor (Scoped)
      → RequestContext (Scoped)   — CorrelationId, UserAgent per request
      → RandomGenerator (Singleton) — thread-safe random delay
      → StatisticsCollector (Singleton) — application-wide metrics
GET /api/orders/stats
  → StatisticsCollector (Singleton)
```

## Run

```bash
dotnet test OrderProcessingApi.sln
dotnet run --project OrderProcessing.Api/OrderProcessing.Api.csproj
# Swagger UI: https://localhost:{port}/swagger
```

---

## Q&A

### 1. Why is RequestContext not Singleton?

RequestContext holds data unique to a single HTTP request: CorrelationId, UserAgent, and StartTime.
All properties are set once in the constructor and never change (immutable).
If registered as Singleton, one instance would be shared across all concurrent requests —
all requests would carry the CorrelationId and UserAgent captured at application startup.
Request A and Request B would be indistinguishable in logs.
In a system with UserId, this would be a critical security vulnerability:
User A could see User B's data.

It must be Scoped so each request gets its own isolated instance, bound to the HttpContext lifecycle.

### 2. When is Singleton dangerous?

Singleton is dangerous in two scenarios:

**Shared mutable state without synchronization:**
Multiple concurrent requests write to the same object simultaneously, causing race conditions.
Example: two threads both read `total=5`, both write `total=6` — one increment is lost.
Solution: use `lock` to ensure atomic updates across related fields.

**Captive Dependency:**
A Singleton captures a Scoped dependency in its constructor. The Scoped object is never released —
it lives for the entire application lifetime instead of per request.
ASP.NET Core throws `InvalidOperationException` in Development (`ValidateScopes=true`),
but is silently broken in Production (`ValidateScopes=false`).

### 3. When is Transient wasteful?

When an object is expensive to construct and resolved multiple times within the same request.
Each resolution creates a new instance — unnecessary allocations and GC pressure.
For stateless, lightweight services Transient is fine.
For services resolved repeatedly in the same pipeline, Scoped is more efficient.

### 4. What bug surprised you most?

The RequestContext-as-Singleton bug. The most insidious aspect was that it bypassed ASP.NET Core's
built-in scope validation entirely. Because a Singleton is technically resolvable from anywhere,
the framework did not throw at startup. The application launched cleanly but silently shared the
same CorrelationId and UserAgent across entirely different incoming requests.

This highlights why validating container registrations in automated tests is critical —
`BugDemoSpecs` proves the bug with `Assert.Same` and catches the fix with `Assert.NotSame`,
catching silent state bleeding before it reaches production.
