using OrderProcessing.Api.Services;

namespace OrderProcessing.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrderProcessingServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IRequestContext, RequestContext>();
        services.AddScoped<IOrderProcessor, OrderProcessor>();
        services.AddSingleton<IStatisticsCollector, StatisticsCollector>();
        services.AddSingleton<IRandomGenerator, RandomGenerator>();
        return services;
    }
}
