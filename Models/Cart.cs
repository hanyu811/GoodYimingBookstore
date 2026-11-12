namespace BookStoreApi.Models;

public class Cart
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ReaderUserName { get; set; } = string.Empty;
    public int BookId { get; set; }
    public int Quantity { get; set; }

    public User? User { get; set; }
    public Book? Book { get; set; }
}
