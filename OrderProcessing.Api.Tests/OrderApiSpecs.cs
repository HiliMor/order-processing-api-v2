using System.Net;
using System.Net.Http.Json;
using OrderProcessing.Api.Contracts;

namespace OrderProcessing.Api.Tests;

public sealed class OrderApiSpecs : IClassFixture<OrderApiFactory>
{
    private readonly HttpClient _client;

    public OrderApiSpecs(OrderApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProcessOrder_ShouldReturn200_WithCorrelationIdAndDuration()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/process")
        {
            Content = JsonContent.Create(new ProcessOrderRequest("order-1"))
        };
        request.Headers.Add("User-Agent", "spec-test-agent");

        using var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<ProcessOrderResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("order-1", payload.OrderId);
        Assert.NotEqual(Guid.Empty, payload.CorrelationId);
        Assert.True(payload.DurationMs > 0);
        Assert.Equal("spec-test-agent", payload.UserAgent);
    }

    [Fact]
    public async Task Stats_ShouldAggregateAcrossParallelRequests()
    {
        var tasks = Enumerable.Range(1, 20)
            .Select(i => _client.PostAsJsonAsync("/api/orders/process", new ProcessOrderRequest($"order-{i}")))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            response.Dispose();
        }

        var stats = await _client.GetFromJsonAsync<OrderStatsResponse>("/api/orders/stats");

        Assert.NotNull(stats);
        Assert.True(stats.TotalOrdersProcessed >= 20);
        Assert.True(stats.AverageProcessingDurationMs > 0);
        Assert.True(stats.LastFiveProcessingDurationsMs.Count <= 5);
    }
}
