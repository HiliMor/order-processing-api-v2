using OrderProcessing.Api.Services;

namespace OrderProcessing.Api.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrderProcessingServices(this IServiceCollection services)
    {
        // Registrations will be added incrementally
        return services;
    }
}
