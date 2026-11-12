namespace BookStoreApi.Contracts;

public record RegisterReaderRequest(
	string UserName,
	string Password,
	string? DisplayName = null,
	string? Address = null,
	string? PhoneNumber = null);
