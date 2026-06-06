using OrderProcessing.Api.Services;

namespace OrderProcessing.Api.Tests;

public sealed class StatisticsCollectorSpecs
{
    [Fact]
    public void Record_SingleDuration_ReturnsCorrectTotalAndAverage()
    {
        var collector = new StatisticsCollector();

        collector.Record(TimeSpan.FromMilliseconds(100));

        var snapshot = collector.GetSnapshot();
        Assert.Equal(1, snapshot.TotalOrdersProcessed);
        Assert.Equal(100.0, snapshot.AverageProcessingDurationMs);
        Assert.Equal(new long[] { 100 }, snapshot.LastFiveProcessingDurationsMs);
    }

    [Fact]
    public void Record_MultipleDurations_ComputesCorrectAverage()
    {
        var collector = new StatisticsCollector();

        collector.Record(TimeSpan.FromMilliseconds(100));
        collector.Record(TimeSpan.FromMilliseconds(200));
        collector.Record(TimeSpan.FromMilliseconds(300));

        var snapshot = collector.GetSnapshot();
        Assert.Equal(3, snapshot.TotalOrdersProcessed);
        Assert.Equal(200.0, snapshot.AverageProcessingDurationMs);
    }

    [Fact]
    public void Record_MoreThanFiveDurations_KeepsOnlyLastFive()
    {
        var collector = new StatisticsCollector();

        foreach (var ms in new[] { 100, 200, 300, 400, 500, 600, 700 })
            collector.Record(TimeSpan.FromMilliseconds(ms));

        var snapshot = collector.GetSnapshot();
        Assert.Equal(7, snapshot.TotalOrdersProcessed);
        Assert.Equal(new long[] { 300, 400, 500, 600, 700 }, snapshot.LastFiveProcessingDurationsMs);
    }

    [Fact]
    public void GetSnapshot_WhenNoRecords_ReturnsZeros()
    {
        var collector = new StatisticsCollector();

        var snapshot = collector.GetSnapshot();

        Assert.Equal(0, snapshot.TotalOrdersProcessed);
        Assert.Equal(0.0, snapshot.AverageProcessingDurationMs);
        Assert.Empty(snapshot.LastFiveProcessingDurationsMs);
    }
}
