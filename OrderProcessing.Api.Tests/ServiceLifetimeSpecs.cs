using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Api.Services;

namespace OrderProcessing.Api.Tests;

public sealed class ServiceLifetimeSpecs : IClassFixture<OrderApiFactory>
{
    private readonly IServiceProvider _services;

    public ServiceLifetimeSpecs(OrderApiFactory factory)
    {
        _services = factory.Services;
    }

    [Fact]
    public void RequestContext_ShouldBeSharedWithinScope_AndIsolatedAcrossScopes()
    {
        using var scope1 = _services.CreateScope();
        using var scope2 = _services.CreateScope();

        var context1a = scope1.ServiceProvider.GetRequiredService<IRequestContext>();
        var context1b = scope1.ServiceProvider.GetRequiredService<IRequestContext>();
        var context2 = scope2.ServiceProvider.GetRequiredService<IRequestContext>();

        Assert.Same(context1a, context1b);
        Assert.NotSame(context1a, context2);
    }

    [Fact]
    public void StatisticsCollector_ShouldBeSharedAcrossAllScopes()
    {
        using var scope1 = _services.CreateScope();
        using var scope2 = _services.CreateScope();

        var stats1 = scope1.ServiceProvider.GetRequiredService<IStatisticsCollector>();
        var stats2 = scope2.ServiceProvider.GetRequiredService<IStatisticsCollector>();

        Assert.Same(stats1, stats2);
    }
}
