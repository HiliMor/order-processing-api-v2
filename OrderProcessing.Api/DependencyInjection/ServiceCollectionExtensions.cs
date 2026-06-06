using System.Threading.RateLimiting;
using OrderProcessing.Api.Contracts;
using OrderProcessing.Api.Services;

namespace OrderProcessing.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrderProcessingOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<OrderProcessingOptions>()
            .Bind(configuration.GetSection(OrderProcessingOptions.SectionName))
            .Validate(o => o.MinDelayMs >= 0, "MinDelayMs must be >= 0.")
            .Validate(o => o.MaxDelayMs > o.MinDelayMs, "MaxDelayMs must be greater than MinDelayMs.")
            .Validate(o => o.RateLimitPerMinute > 0, "RateLimitPerMinute must be > 0.")
            .ValidateOnStart();
        return services;
    }

    public static IServiceCollection AddOrderProcessingServices(this IServiceCollection services)
    {
        services.AddMetrics();
        services.AddHttpContextAccessor();
        services.AddScoped<IRequestContext, RequestContext>();
        services.AddScoped<IOrderProcessor, OrderProcessor>();
        services.AddSingleton<IStatisticsCollector, StatisticsCollector>();
        services.AddSingleton<IOrderMetrics, OrderMetrics>();
        services.AddSingleton<IRandomGenerator, RandomGenerator>();
        return services;
    }

    public static IServiceCollection AddOrderProcessingRateLimiter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rateLimitPerMinute = configuration
            .GetSection(OrderProcessingOptions.SectionName)
            .GetValue<int>("RateLimitPerMinute", 100);

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(OrderProcessingOptions.ProcessOrderRateLimitPolicy, _ =>
                RateLimitPartition.GetFixedWindowLimiter(
                    OrderProcessingOptions.ProcessOrderRateLimitPolicy,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitPerMinute,
                        Window = TimeSpan.FromMinutes(1)
                    }));
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }
}
