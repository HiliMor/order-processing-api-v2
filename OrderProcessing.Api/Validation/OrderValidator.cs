namespace OrderProcessing.Api.Validation;

public static class OrderValidator
{
    public const int MaxOrderIdLength = 256;

    public static bool IsValidOrderId(string? orderId)
    {
        return !string.IsNullOrWhiteSpace(orderId) && orderId.Length <= MaxOrderIdLength;
    }
}
