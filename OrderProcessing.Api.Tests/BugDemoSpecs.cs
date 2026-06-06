using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderProcessing.Api.Services;

namespace OrderProcessing.Api.Tests;

public sealed class BugDemoSpecs
{
    [Fact]
    public void WrongLifetime_RequestContext_AsSingleton_SharesInstanceAcrossScopes()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("environment", "Testing");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IRequestContext>();
                    services.AddSingleton<IRequestContext, RequestContext>();
                });
            });

        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();

        var context1 = scope1.ServiceProvider.GetRequiredService<IRequestContext>();
        var context2 = scope2.ServiceProvider.GetRequiredService<IRequestContext>();

        Assert.Same(context1, context2);
        Assert.Equal(context1.CorrelationId, context2.CorrelationId);
    }

    [Fact]
    public void FixedLifetime_RequestContext_AsScoped_IsolatesInstancesAcrossScopes()
    {
        using var factory = new OrderApiFactory();

        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();

        var context1 = scope1.ServiceProvider.GetRequiredService<IRequestContext>();
        var context2 = scope2.ServiceProvider.GetRequiredService<IRequestContext>();

        Assert.NotSame(context1, context2);
        Assert.NotEqual(context1.CorrelationId, context2.CorrelationId);
    }
}
