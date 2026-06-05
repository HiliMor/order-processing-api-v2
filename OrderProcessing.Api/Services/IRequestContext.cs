namespace OrderProcessing.Api.Services;

public interface IRequestContext
{
    Guid CorrelationId { get; }
    string UserAgent { get; }
    DateTime StartTimeUtc { get; }
}
