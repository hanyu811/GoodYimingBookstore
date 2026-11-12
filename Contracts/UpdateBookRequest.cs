namespace BookStoreApi.Contracts;

public record UpdateBookRequest(
    string Title,
    string? Author,
    string? Category,
    decimal Price,
    int Stock,
    string? Description,
    string? ImageUrl);
