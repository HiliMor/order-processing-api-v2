using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderProcessing.Api.Contracts;
using OrderProcessing.Api.Services;

namespace OrderProcessing.Api.Tests;

public sealed class ErrorHandlingSpecs
{
    [Fact]
    public async Task ProcessOrder_WhenProcessorThrows_ReturnsGeneric500WithResponseHeaders()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("environment", "Testing");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IOrderProcessor>();
                    services.AddScoped<IOrderProcessor, ThrowingOrderProcessor>();
                });
            });
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/orders/process",
            new ProcessOrderRequest("order-failure"));
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Unexpected server error.", payload["error"]);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
    }

    private sealed class ThrowingOrderProcessor : IOrderProcessor
    {
        public Task<ProcessOrderResponse> ProcessAsync(
            ProcessOrderRequest request,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Sensitive implementation detail.");
        }
    }
}
