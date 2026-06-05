using OrderProcessing.Api.Contracts;

namespace OrderProcessing.Api.Services;

public interface IOrderProcessor
{
    Task<ProcessOrderResponse> ProcessAsync(ProcessOrderRequest request, CancellationToken cancellationToken);
}
