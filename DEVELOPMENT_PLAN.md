# Development Plan

Incremental build plan — each commit adds one focused concern.

## Step 1 — Scaffold
Solution structure, contracts (request/response records), and service interfaces.
No implementation yet. Establishes the public API surface before writing any logic.

Files: `Contracts/`, `Services/I*.cs`, `DependencyInjection/ServiceCollectionExtensions.cs`, `Program.cs`

## Step 2 — Architecture Decisions
Document lifetime choices, thread safety strategy, validation approach, and
intentional bug demonstration before writing any implementation code.

Files: `ARCHITECTURE_DECISIONS.md`, `DEVELOPMENT_PLAN.md`

## Step 3 — Core Services
Implement RequestContext, OrderProcessor, StatisticsCollector, RandomGenerator.
Register all services in DI container with correct lifetimes.

Files: `Services/*.cs`, `DependencyInjection/ServiceCollectionExtensions.cs`

## Step 4 — Configuration (IOptions)
Add OrderProcessingOptions with MinDelayMs/MaxDelayMs.
Add ValidateOnStart to catch misconfiguration at startup.

Files: `Contracts/OrderProcessingOptions.cs`, `appsettings.json`, `Program.cs`

## Step 5 — Input Validation
Extract orderId validation to a dedicated OrderValidator class.
Keep Program.cs clean — wiring only, no business logic.

Files: `Validation/OrderValidator.cs`, `Program.cs`

## Step 6 — Rate Limiting
Add fixed-window rate limiting using .NET 8 built-in middleware.
Protects the endpoint from abuse without external dependencies.

Files: `Program.cs`

## Step 7 — Security
Add HTTPS redirection, security response headers (X-Content-Type-Options, X-Frame-Options),
and suppress Server header via Kestrel. Add structured logging to OrderProcessor.

Files: `Program.cs`, `Services/OrderProcessor.cs`

## Step 8 — Integration Tests
Test the API end-to-end over HTTP using WebApplicationFactory.

Coverage targets:
- POST /api/orders/process — valid request returns 200 with CorrelationId and DurationMs
- POST /api/orders/process — 20 parallel requests all return 200 (concurrency)
- GET /api/orders/stats — TotalOrdersProcessed >= 20, average > 0, last five <= 5

Files: `Tests/OrderApiSpecs.cs`, `Tests/OrderApiFactory.cs`

## Step 9 — Lifetime Tests
Verify DI container registrations directly — without HTTP.

Coverage targets:
- RequestContext: same instance within scope, different instance across scopes (Scoped)
- StatisticsCollector: same instance across all scopes (Singleton)

Files: `Tests/ServiceLifetimeSpecs.cs`

## Step 10 — Bug Demonstration
Demonstrate RequestContext-as-Singleton bug and its fix.

Coverage targets:
- Wrong lifetime: Assert.Same (same object) + Assert.Equal (same CorrelationId)
- Fixed lifetime: Assert.NotSame (different objects) + Assert.NotEqual (different CorrelationId)

Files: `Tests/BugDemoSpecs.cs`

## Step 11 — Edge Case Tests
Cover input validation branches and zero-stats baseline.

Coverage targets:
- OrderValidator: empty orderId → 400 (IsNullOrWhiteSpace branch)
- OrderValidator: orderId > 256 chars → 400 (length branch)
- StatisticsCollector: GetSnapshot with zero orders → average=0 branch
- StatsBaselineSpecs isolated in its own class → fresh StatisticsCollector Singleton

Files: `Tests/StatsEdgeCaseSpecs.cs`

---

## Test Coverage Strategy

All branches in production code must be exercised by at least one test:

| Branch | Covered by |
|---|---|
| OrderValidator — null/empty orderId | StatsEdgeCaseSpecs |
| OrderValidator — orderId too long | StatsEdgeCaseSpecs |
| OrderValidator — valid orderId | OrderApiSpecs |
| StatisticsCollector.GetSnapshot — zero orders (average=0) | StatsBaselineSpecs |
| StatisticsCollector.GetSnapshot — with orders | OrderApiSpecs |
| StatisticsCollector.Record — queue trim at 5 | OrderApiSpecs (parallel) |
| RequestContext — Scoped isolation | ServiceLifetimeSpecs |
| StatisticsCollector — Singleton sharing | ServiceLifetimeSpecs |
| OrderMetrics — Singleton sharing | ServiceLifetimeSpecs |
| Wrong lifetime bug | BugDemoSpecs |
| Fixed lifetime | BugDemoSpecs |
| OrderProcessor — success, cancellation, and failure outcomes | OrderProcessorSpecs |
