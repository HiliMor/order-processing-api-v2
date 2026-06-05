namespace OrderProcessing.Api.Services;

public interface IRandomGenerator
{
    int NextDurationMs(int minInclusive, int maxExclusive);
}
