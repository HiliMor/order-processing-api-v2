namespace OrderProcessing.Api.Contracts;

public sealed class OrderProcessingOptions
{
    public const string SectionName = "OrderProcessing";
    public int MinDelayMs { get; init; }
    public int MaxDelayMs { get; init; }
    public int RateLimitPerMinute { get; init; } = 100;

    public const string ProcessOrderRateLimitPolicy = "process-order";
}
