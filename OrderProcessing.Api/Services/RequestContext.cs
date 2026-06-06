namespace OrderProcessing.Api.Services;

public sealed class RequestContext : IRequestContext
{
    private const int MaxUserAgentLength = 256;

    public Guid CorrelationId { get; } = Guid.NewGuid();
    public DateTime StartTimeUtc { get; } = DateTime.UtcNow;
    public string UserAgent { get; }

    public RequestContext(IHttpContextAccessor httpContextAccessor)
    {
        var raw = httpContextAccessor.HttpContext?
            .Request.Headers.UserAgent.ToString();

        if (string.IsNullOrWhiteSpace(raw))
        {
            UserAgent = "unknown";
        }
        else
        {
            UserAgent = raw.Length > MaxUserAgentLength
                ? raw[..MaxUserAgentLength]
                : raw;
        }
    }
}
