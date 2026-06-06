using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Api.Services;

namespace OrderProcessing.Api.Tests;

public sealed class OrderMetricsSpecs
{
    [Theory]
    [InlineData("success", 125)]
    [InlineData("cancelled", 50)]
    [InlineData("failed", 75)]
    public void RecordOutcome_EmitsOperationCountAndDuration(string outcome, long durationMs)
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddSingleton<IOrderMetrics, OrderMetrics>();

        using var provider = services.BuildServiceProvider();
        var meterFactory = provider.GetRequiredService<IMeterFactory>();
        var measurements = new List<MetricMeasurement>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == OrderMetrics.MeterName &&
                ReferenceEquals(instrument.Meter.Scope, meterFactory))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            string? outcomeTag = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "outcome")
                {
                    outcomeTag = tag.Value?.ToString();
                    break;
                }
            }

            measurements.Add(new MetricMeasurement(instrument.Name, measurement, outcomeTag));
        });
        listener.Start();

        var metrics = provider.GetRequiredService<IOrderMetrics>();
        Record(metrics, outcome, durationMs);

        Assert.Contains(measurements, measurement =>
            measurement.Name == OrderMetrics.OperationsCounterName &&
            measurement.Value == 1 &&
            measurement.Outcome == outcome);
        Assert.Contains(measurements, measurement =>
            measurement.Name == OrderMetrics.DurationHistogramName &&
            measurement.Value == durationMs &&
            measurement.Outcome == outcome);
    }

    private static void Record(IOrderMetrics metrics, string outcome, long durationMs)
    {
        switch (outcome)
        {
            case "success":
                metrics.RecordSuccess(durationMs);
                break;
            case "cancelled":
                metrics.RecordCancelled(durationMs);
                break;
            case "failed":
                metrics.RecordFailed(durationMs);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown metric outcome.");
        }
    }

    private sealed record MetricMeasurement(string Name, long Value, string? Outcome);
}
