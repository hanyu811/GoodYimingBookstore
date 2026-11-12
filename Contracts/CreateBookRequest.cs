namespace BookStoreApi.Contracts;

public record CreateBookRequest(
    string Title,
    string? Author,
    string? Category,
    decimal Price,
    int Stock,
    string? Description,
    string? ImageUrl);
