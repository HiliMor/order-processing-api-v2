namespace OrderProcessing.Api.Services;

public sealed class RandomGenerator : IRandomGenerator
{
    public int NextDurationMs(int minInclusive, int maxExclusive)
    {
        return Random.Shared.Next(minInclusive, maxExclusive);
    }
}
