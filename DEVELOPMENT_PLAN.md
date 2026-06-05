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

## Step 7 — Security Headers
Remove Server header (already done via Kestrel config).
Add X-Content-Type-Options and X-Frame-Options response headers.

Files: `Program.cs`

## Step 8 — Structured Logging
Add ILogger to OrderProcessor. Log correlation ID, order ID, and duration
on each processed order for observability.

Files: `Services/OrderProcessor.cs`

## Step 9 — Integration Tests
Test the API end-to-end over HTTP using WebApplicationFactory.
Cover happy path, parallel requests, and stats aggregation.

Files: `Tests/OrderApiSpecs.cs`, `Tests/OrderApiFactory.cs`

## Step 10 — Lifetime Tests
Verify DI container registrations directly — without HTTP.
Assert Scoped isolation and Singleton sharing.

Files: `Tests/ServiceLifetimeSpecs.cs`

## Step 11 — Bug Demonstration
Demonstrate RequestContext-as-Singleton bug and its fix.
Assert.Same proves shared instance; Assert.NotSame proves isolation after fix.

Files: `Tests/BugDemoSpecs.cs`

## Step 12 — Edge Case Tests
Cover empty orderId, oversized orderId, and zero-stats baseline.
StatsBaselineSpecs in a separate class to ensure a fresh StatisticsCollector.

Files: `Tests/StatsEdgeCaseSpecs.cs`
