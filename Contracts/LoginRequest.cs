namespace BookStoreApi.Contracts;

public record LoginRequest(string UserName, string Password, string? Role = "reader");
