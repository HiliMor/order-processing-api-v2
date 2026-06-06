using System.Diagnostics;
using OrderProcessing.Api.Contracts;
using Microsoft.Extensions.Options;

namespace OrderProcessing.Api.Services;

public sealed class OrderProcessor : IOrderProcessor
{
    private readonly IRequestContext _requestContext;
    private readonly IRandomGenerator _randomGenerator;
    private readonly IStatisticsCollector _statisticsCollector;
    private readonly OrderProcessingOptions _options;
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(
        IRequestContext requestContext,
        IRandomGenerator randomGenerator,
        IStatisticsCollector statisticsCollector,
        IOptions<OrderProcessingOptions> options,
        ILogger<OrderProcessor> logger)
    {
        _requestContext = requestContext;
        _randomGenerator = randomGenerator;
        _statisticsCollector = statisticsCollector;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProcessOrderResponse> ProcessAsync(ProcessOrderRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing order {OrderId} [CorrelationId={CorrelationId}]",
            request.OrderId, _requestContext.CorrelationId);

        var delayMs = _randomGenerator.NextDurationMs(_options.MinDelayMs, _options.MaxDelayMs);

        var stopwatch = Stopwatch.StartNew();
        await Task.Delay(delayMs, cancellationToken);
        stopwatch.Stop();

        _statisticsCollector.Record(stopwatch.Elapsed);

        _logger.LogInformation("Order {OrderId} completed in {DurationMs}ms [CorrelationId={CorrelationId}]",
            request.OrderId, stopwatch.ElapsedMilliseconds, _requestContext.CorrelationId);

        return new ProcessOrderResponse(
            request.OrderId,
            _requestContext.CorrelationId,
            _requestContext.UserAgent,
            stopwatch.ElapsedMilliseconds);
    }
}
