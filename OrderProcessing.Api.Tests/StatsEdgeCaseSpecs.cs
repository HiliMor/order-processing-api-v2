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

    [Fact]
    public async Task ProcessOrder_ShouldReturn200_WhenOrderIdIsExactly256Characters()
    {
        var maxOrderId = new string('x', 256);
        var response = await _client.PostAsJsonAsync(
            "/api/orders/process",
            new ProcessOrderRequest(maxOrderId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProcessOrder_ShouldReturnUniqueCorrelationId_PerRequest()
    {
        var response1 = await _client.PostAsJsonAsync("/api/orders/process", new ProcessOrderRequest("order-a"));
        var response2 = await _client.PostAsJsonAsync("/api/orders/process", new ProcessOrderRequest("order-b"));

        var payload1 = await response1.Content.ReadFromJsonAsync<ProcessOrderResponse>();
        var payload2 = await response2.Content.ReadFromJsonAsync<ProcessOrderResponse>();

        Assert.NotNull(payload1);
        Assert.NotNull(payload2);
        Assert.NotEqual(payload1.CorrelationId, payload2.CorrelationId);
    }

    [Fact]
    public async Task ProcessOrder_ShouldReturnUnknownUserAgent_WhenNotProvided()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/orders/process",
            new ProcessOrderRequest("order-no-agent"));

        var payload = await response.Content.ReadFromJsonAsync<ProcessOrderResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("unknown", payload.UserAgent);
    }

    [Fact]
    public async Task ProcessOrder_ShouldTruncateUserAgent_WhenItExceeds256Characters()
    {
        var longUserAgent = new string('a', 300);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/process")
        {
            Content = JsonContent.Create(new ProcessOrderRequest("order-long-agent"))
        };
        request.Headers.Add("User-Agent", longUserAgent);

        using var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<ProcessOrderResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(longUserAgent[..256], payload.UserAgent);
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
