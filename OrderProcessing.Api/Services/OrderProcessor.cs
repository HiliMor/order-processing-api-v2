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

    public OrderProcessor(
        IRequestContext requestContext,
        IRandomGenerator randomGenerator,
        IStatisticsCollector statisticsCollector,
        IOptions<OrderProcessingOptions> options)
    {
        _requestContext = requestContext;
        _randomGenerator = randomGenerator;
        _statisticsCollector = statisticsCollector;
        _options = options.Value;
    }

    public async Task<ProcessOrderResponse> ProcessAsync(ProcessOrderRequest request, CancellationToken cancellationToken)
    {
        var delayMs = _randomGenerator.NextDurationMs(_options.MinDelayMs, _options.MaxDelayMs);

        var stopwatch = Stopwatch.StartNew();
        await Task.Delay(delayMs, cancellationToken);
        stopwatch.Stop();

        _statisticsCollector.Record(stopwatch.Elapsed);

        return new ProcessOrderResponse(
            request.OrderId,
            _requestContext.CorrelationId,
            _requestContext.UserAgent,
            stopwatch.ElapsedMilliseconds);
    }
}
