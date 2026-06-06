using System.Diagnostics.Metrics;

namespace OrderProcessing.Api.Services;

public sealed class OrderMetrics : IOrderMetrics
{
    public const string MeterName = "OrderProcessing.Api";
    public const string OperationsCounterName = "order_processing.operations";
    public const string DurationHistogramName = "order_processing.duration";

    private readonly Counter<long> _operations;
    private readonly Histogram<long> _processingDuration;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _operations = meter.CreateCounter<long>(
            OperationsCounterName,
            unit: "{operation}",
            description: "Number of order processing operations, tagged by outcome.");
        _processingDuration = meter.CreateHistogram<long>(
            DurationHistogramName,
            unit: "ms",
            description: "Elapsed duration of order processing operations, tagged by outcome.");
    }

    public void RecordSuccess(long durationMs) => Record("success", durationMs);
    public void RecordCancelled(long durationMs) => Record("cancelled", durationMs);
    public void RecordFailed(long durationMs) => Record("failed", durationMs);

    private void Record(string outcome, long durationMs)
    {
        var tag = new KeyValuePair<string, object?>("outcome", outcome);
        _operations.Add(1, tag);
        _processingDuration.Record(durationMs, tag);
    }
}
