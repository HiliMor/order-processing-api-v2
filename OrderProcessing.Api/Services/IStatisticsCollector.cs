namespace OrderProcessing.Api.Services;

public interface IStatisticsCollector
{
    void Record(TimeSpan processingDuration);
    StatisticsSnapshot GetSnapshot();
}

public sealed record StatisticsSnapshot(
    long TotalOrdersProcessed,
    double AverageProcessingDurationMs,
    IReadOnlyList<long> LastFiveProcessingDurationsMs);
