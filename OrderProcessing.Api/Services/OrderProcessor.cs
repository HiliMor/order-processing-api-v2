using System.Diagnostics;
using OrderProcessing.Api.Contracts;
using Microsoft.Extensions.Options;

namespace OrderProcessing.Api.Services;

public sealed class OrderProcessor : IOrderProcessor
{
    private readonly IRequestContext _requestContext;
    private readonly IRandomGenerator _randomGenerator;
    private readonly IStatisticsCollector _statisticsCollector;
    private readonly IOrderMetrics _orderMetrics;
    private readonly OrderProcessingOptions _options;
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(
        IRequestContext requestContext,
        IRandomGenerator randomGenerator,
        IStatisticsCollector statisticsCollector,
        IOrderMetrics orderMetrics,
        IOptions<OrderProcessingOptions> options,
        ILogger<OrderProcessor> logger)
    {
        _requestContext = requestContext;
        _randomGenerator = randomGenerator;
        _statisticsCollector = statisticsCollector;
        _orderMetrics = orderMetrics;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProcessOrderResponse> ProcessAsync(ProcessOrderRequest request, CancellationToken cancellationToken)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = _requestContext.CorrelationId,
            ["OrderId"] = request.OrderId
        });

        _logger.LogInformation("Processing order started.");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var delayMs = _randomGenerator.NextDurationMs(_options.MinDelayMs, _options.MaxDelayMs);
            await Task.Delay(delayMs, cancellationToken);
            stopwatch.Stop();

            _statisticsCollector.Record(stopwatch.Elapsed);
            _orderMetrics.RecordSuccess(stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("Order completed in {DurationMs}ms.", stopwatch.ElapsedMilliseconds);

            return new ProcessOrderResponse(
                request.OrderId,
                _requestContext.CorrelationId,
                _requestContext.UserAgent,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _orderMetrics.RecordCancelled(stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Order cancelled by client after {DurationMs}ms.", stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _orderMetrics.RecordFailed(stopwatch.ElapsedMilliseconds);
            _logger.LogError(ex, "Order failed after {DurationMs}ms.", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
