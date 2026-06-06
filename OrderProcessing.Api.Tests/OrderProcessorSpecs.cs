using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderProcessing.Api.Contracts;
using OrderProcessing.Api.Services;

namespace OrderProcessing.Api.Tests;

public sealed class OrderProcessorSpecs
{
    private static readonly ProcessOrderRequest Request = new("order-1");

    [Fact]
    public async Task ProcessAsync_WhenSuccessful_RecordsStatisticsAndSuccessMetric()
    {
        var statistics = new RecordingStatisticsCollector();
        var metrics = new RecordingOrderMetrics();
        var processor = CreateProcessor(
            new FixedRandomGenerator(1),
            statistics,
            metrics);

        var response = await processor.ProcessAsync(Request, CancellationToken.None);

        Assert.Equal(Request.OrderId, response.OrderId);
        Assert.Equal(1, statistics.RecordCount);
        Assert.Equal(1, metrics.SuccessCount);
        Assert.Equal(0, metrics.CancelledCount);
        Assert.Equal(0, metrics.FailedCount);
    }

    [Fact]
    public async Task ProcessAsync_WhenCancelled_RecordsOnlyCancellationMetric()
    {
        var statistics = new RecordingStatisticsCollector();
        var metrics = new RecordingOrderMetrics();
        var processor = CreateProcessor(
            new FixedRandomGenerator(1_000),
            statistics,
            metrics);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => processor.ProcessAsync(Request, cancellation.Token));

        Assert.Equal(0, statistics.RecordCount);
        Assert.Equal(0, metrics.SuccessCount);
        Assert.Equal(1, metrics.CancelledCount);
        Assert.Equal(0, metrics.FailedCount);
    }

    [Fact]
    public async Task ProcessAsync_WhenDependencyFails_RecordsOnlyFailureMetric()
    {
        var statistics = new RecordingStatisticsCollector();
        var metrics = new RecordingOrderMetrics();
        var processor = CreateProcessor(
            new ThrowingRandomGenerator(),
            statistics,
            metrics);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => processor.ProcessAsync(Request, CancellationToken.None));

        Assert.Equal(0, statistics.RecordCount);
        Assert.Equal(0, metrics.SuccessCount);
        Assert.Equal(0, metrics.CancelledCount);
        Assert.Equal(1, metrics.FailedCount);
    }

    private static OrderProcessor CreateProcessor(
        IRandomGenerator randomGenerator,
        IStatisticsCollector statistics,
        IOrderMetrics metrics)
    {
        var requestContext = new StubRequestContext();
        var options = Options.Create(new OrderProcessingOptions
        {
            MinDelayMs = 0,
            MaxDelayMs = 10
        });

        return new OrderProcessor(
            requestContext,
            randomGenerator,
            statistics,
            metrics,
            options,
            NullLogger<OrderProcessor>.Instance);
    }

    private sealed class StubRequestContext : IRequestContext
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public string UserAgent => "test-agent";
        public DateTime StartTimeUtc { get; } = DateTime.UtcNow;
    }

    private sealed class FixedRandomGenerator(int durationMs) : IRandomGenerator
    {
        public int NextDurationMs(int minInclusive, int maxExclusive) => durationMs;
    }

    private sealed class ThrowingRandomGenerator : IRandomGenerator
    {
        public int NextDurationMs(int minInclusive, int maxExclusive)
        {
            throw new InvalidOperationException("Simulated dependency failure.");
        }
    }

    private sealed class RecordingStatisticsCollector : IStatisticsCollector
    {
        public int RecordCount { get; private set; }

        public void Record(TimeSpan processingDuration)
        {
            RecordCount++;
        }

        public StatisticsSnapshot GetSnapshot()
        {
            return new StatisticsSnapshot(RecordCount, 0, []);
        }
    }

    private sealed class RecordingOrderMetrics : IOrderMetrics
    {
        public int SuccessCount { get; private set; }
        public int CancelledCount { get; private set; }
        public int FailedCount { get; private set; }

        public void RecordSuccess(long durationMs)
        {
            SuccessCount++;
        }

        public void RecordCancelled(long durationMs)
        {
            CancelledCount++;
        }

        public void RecordFailed(long durationMs)
        {
            FailedCount++;
        }
    }
}
