namespace OrderProcessing.Api.Services;

public sealed class StatisticsCollector : IStatisticsCollector
{
    private const int MaxRecentDurations = 5;
    private readonly object _lock = new();
    private readonly Queue<long> _lastFiveProcessingDurationsMs = new();
    private long _totalOrdersProcessed;
    private long _totalProcessingDurationMs;

    public void Record(TimeSpan processingDuration)
    {
        var durationMs = Math.Max(1, (long)Math.Round(processingDuration.TotalMilliseconds));
        lock (_lock)
        {
            _totalOrdersProcessed++;
            _totalProcessingDurationMs += durationMs;
            _lastFiveProcessingDurationsMs.Enqueue(durationMs);
            if (_lastFiveProcessingDurationsMs.Count > MaxRecentDurations)
                _lastFiveProcessingDurationsMs.Dequeue();
        }
    }

    public StatisticsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var average = _totalOrdersProcessed == 0
                ? 0
                : (double)_totalProcessingDurationMs / _totalOrdersProcessed;
            return new StatisticsSnapshot(
                _totalOrdersProcessed,
                average,
                _lastFiveProcessingDurationsMs.ToArray());
        }
    }
}
