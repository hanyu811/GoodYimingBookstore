namespace BookStoreApi.Contracts;

public record UpdateUserRequest(string Role, string? DisplayName, string? Password);
