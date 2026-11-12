namespace BookStoreApi.Models;

public static class OrderStatus
{
    public const string Unshipped = "Unshipped";
    public const string Shipped = "Shipped";
    public const string Received = "Received";

    public static bool IsValid(string? status)
    {
        return status is Unshipped or Shipped or Received;
    }
}
