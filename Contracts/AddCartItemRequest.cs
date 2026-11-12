namespace BookStoreApi.Contracts;

public record AddCartItemRequest(int BookId, int Quantity = 1);
