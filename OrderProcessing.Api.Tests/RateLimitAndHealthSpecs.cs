using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using OrderProcessing.Api.Contracts;
using OrderProcessing.Api.Services;

namespace OrderProcessing.Api.Tests;

public sealed class RateLimitAndHealthSpecs : IClassFixture<OrderApiFactory>
{
    private readonly HttpClient _client;

    public RateLimitAndHealthSpecs(OrderApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ShouldReturn200()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_ShouldRemainAccessible_WhenOrdersAreRateLimited()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("environment", "Testing");
                builder.UseSetting($"{OrderProcessingOptions.SectionName}:RateLimitPerMinute", "1");
                builder.UseSetting($"{OrderProcessingOptions.SectionName}:MinDelayMs", "0");
                builder.UseSetting($"{OrderProcessingOptions.SectionName}:MaxDelayMs", "1");
            });

        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/orders/process", new ProcessOrderRequest("order-1"));
        var rateLimitedResponse = await client.PostAsJsonAsync("/api/orders/process", new ProcessOrderRequest("order-2"));
        var healthResponse = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.TooManyRequests, rateLimitedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
    }

    [Fact]
    public async Task ProcessOrder_ShouldReturn429_WhenRateLimitExceeded()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("environment", "Testing");
                builder.UseSetting($"{OrderProcessingOptions.SectionName}:RateLimitPerMinute", "1");
                builder.UseSetting($"{OrderProcessingOptions.SectionName}:MinDelayMs", "0");
                builder.UseSetting($"{OrderProcessingOptions.SectionName}:MaxDelayMs", "1");
            });

        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/orders/process", new ProcessOrderRequest("order-1"));
        var response = await client.PostAsJsonAsync("/api/orders/process", new ProcessOrderRequest("order-2"));

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}

public sealed class StatisticsCollectorConcurrencySpecs
{
    [Fact]
    public async Task StatisticsCollector_ShouldRecordExactCount_UnderHighConcurrency()
    {
        var collector = new StatisticsCollector();
        const int threadCount = 100;

        var tasks = Enumerable.Range(0, threadCount)
            .Select(_ => Task.Run(() => collector.Record(TimeSpan.FromMilliseconds(10))));

        await Task.WhenAll(tasks);

        var snapshot = collector.GetSnapshot();
        Assert.Equal(threadCount, snapshot.TotalOrdersProcessed);
        Assert.True(snapshot.LastFiveProcessingDurationsMs.Count <= 5);
    }
}
