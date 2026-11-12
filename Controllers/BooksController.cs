using BookStoreApi.Contracts;
using BookStoreApi.Data;
using BookStoreApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStoreApi.Controllers;

[ApiController]
[Route("api")]
public class BooksController : ControllerBase
{
    private readonly BookStoreDbContext _db;

    public BooksController(BookStoreDbContext db)
    {
        _db = db;
    }

    [HttpGet("books")]
    public async Task<IActionResult> GetBooks([FromQuery] string? keyword)
    {
        var query = _db.Books.Where(b => b.IsListed).AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(b =>
                b.Title.Contains(keyword) ||
                (b.Author != null && b.Author.Contains(keyword)));
        }

        var result = await query.OrderByDescending(b => b.Id).ToListAsync();
        return Ok(result);
    }

    [HttpGet("books/{id:int}")]
    public async Task<IActionResult> GetBookById(int id)
    {
        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == id && b.IsListed);
        return book is null ? NotFound(new { message = "图书不存在" }) : Ok(book);
    }

    [HttpPost("books")]
    public async Task<IActionResult> CreateBook([FromBody] CreateBookRequest request)
    {
        var book = new Book
        {
            Title = request.Title,
            Author = request.Author,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "未分类" : request.Category,
            Price = request.Price,
            Stock = request.Stock,
            Description = request.Description,
            CoverUrl = request.ImageUrl ?? string.Empty,
            ImageUrl = request.ImageUrl,
            IsListed = true
        };

        _db.Books.Add(book);
        await _db.SaveChangesAsync();
        return Created($"/api/books/{book.Id}", book);
    }

    [HttpGet("categories")]
    public IActionResult GetCategories()
    {
        return Ok(Array.Empty<string>());
    }

    [HttpGet("docs")]
    public IActionResult GetDocsLinks()
    {
        return Ok(new
        {
            projectGuide = "/docs/项目说明.md",
            apiGuide = "/docs/接口文档.md",
            assignment = "/docs/课程实践任务.pdf",
            swagger = "/swagger"
        });
    }
}
