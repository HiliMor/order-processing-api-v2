namespace OrderProcessing.Api.Contracts;

public sealed record ProcessOrderResponse(
    string OrderId,
    Guid CorrelationId,
    string UserAgent,
    long DurationMs);
