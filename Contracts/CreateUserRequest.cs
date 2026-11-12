namespace BookStoreApi.Contracts;

public record CreateUserRequest(string UserName, string Password, string Role, string? DisplayName);
