namespace BookStoreApi.Models;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string Category { get; set; } = "未分类";
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string? Description { get; set; }
    public string CoverUrl { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsListed { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
