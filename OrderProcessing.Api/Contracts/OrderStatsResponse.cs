namespace OrderProcessing.Api.Contracts;

public sealed record OrderStatsResponse(
    long TotalOrdersProcessed,
    double AverageProcessingDurationMs,
    IReadOnlyList<long> LastFiveProcessingDurationsMs);
