namespace OrderProcessing.Api.Services;

public sealed class RequestContext : IRequestContext
{
    public Guid CorrelationId { get; } = Guid.NewGuid();
    public DateTime StartTimeUtc { get; } = DateTime.UtcNow;
    public string UserAgent { get; }

    public RequestContext(IHttpContextAccessor httpContextAccessor)
    {
        UserAgent = httpContextAccessor.HttpContext?
            .Request.Headers.UserAgent.ToString() ?? string.Empty;
    }
}
