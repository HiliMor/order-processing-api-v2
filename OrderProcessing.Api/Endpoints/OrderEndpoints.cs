using OrderProcessing.Api.Contracts;
using OrderProcessing.Api.Services;
using OrderProcessing.Api.Validation;

namespace OrderProcessing.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderProcessingEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");

        app.MapPost("/api/orders/process", async (
            ProcessOrderRequest request,
            IOrderProcessor orderProcessor,
            CancellationToken cancellationToken) =>
        {
            if (!OrderValidator.IsValidOrderId(request.OrderId))
                return Results.BadRequest(new { error = $"orderId is required and must not exceed {OrderValidator.MaxOrderIdLength} characters." });

            var result = await orderProcessor.ProcessAsync(request, cancellationToken);
            return Results.Ok(result);
        }).RequireRateLimiting(OrderProcessingOptions.ProcessOrderRateLimitPolicy);

        app.MapGet("/api/orders/stats", (IStatisticsCollector statisticsCollector) =>
        {
            var snapshot = statisticsCollector.GetSnapshot();
            return Results.Ok(new OrderStatsResponse(
                snapshot.TotalOrdersProcessed,
                snapshot.AverageProcessingDurationMs,
                snapshot.LastFiveProcessingDurationsMs));
        });
    }
}
