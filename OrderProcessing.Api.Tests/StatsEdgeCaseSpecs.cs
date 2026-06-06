using System.Net;
using System.Net.Http.Json;
using OrderProcessing.Api.Contracts;

namespace OrderProcessing.Api.Tests;

public sealed class StatsEdgeCaseSpecs : IClassFixture<OrderApiFactory>
{
    private readonly HttpClient _client;

    public StatsEdgeCaseSpecs(OrderApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProcessOrder_ShouldReturn400_WhenOrderIdIsEmpty()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/orders/process",
            new ProcessOrderRequest(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProcessOrder_ShouldReturn400_WhenOrderIdExceeds256Characters()
    {
        var longOrderId = new string('x', 257);
        var response = await _client.PostAsJsonAsync(
            "/api/orders/process",
            new ProcessOrderRequest(longOrderId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

// Isolated in its own class so the StatisticsCollector Singleton starts fresh,
// independent of any other test class that may record orders.
public sealed class StatsBaselineSpecs : IClassFixture<OrderApiFactory>
{
    private readonly HttpClient _client;

    public StatsBaselineSpecs(OrderApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Stats_ShouldReturnZeros_WhenNoOrdersProcessed()
    {
        var stats = await _client.GetFromJsonAsync<OrderStatsResponse>("/api/orders/stats");

        Assert.NotNull(stats);
        Assert.Equal(0, stats.TotalOrdersProcessed);
        Assert.Equal(0, stats.AverageProcessingDurationMs);
        Assert.Empty(stats.LastFiveProcessingDurationsMs);
    }
}
