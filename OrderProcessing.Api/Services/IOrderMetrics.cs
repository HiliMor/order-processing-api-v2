namespace OrderProcessing.Api.Services;

public interface IOrderMetrics
{
    void RecordSuccess(long durationMs);
    void RecordCancelled(long durationMs);
    void RecordFailed(long durationMs);
}
