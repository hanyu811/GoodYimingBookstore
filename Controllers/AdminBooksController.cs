using BookStoreApi.Contracts;
using BookStoreApi.Data;
using BookStoreApi.Models;
using BookStoreApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStoreApi.Controllers;

[ApiController]
[Route("api/admin/books")]
public class AdminBooksController : ControllerBase
{
    private readonly BookStoreDbContext _db;
    private readonly AuthTokenStore _tokenStore;

    public AdminBooksController(BookStoreDbContext db, AuthTokenStore tokenStore)
    {
        _db = db;
        _tokenStore = tokenStore;
    }

    private bool IsAdmin()
    {
        var token = Request.Headers["X-Auth-Token"].ToString();
        return _tokenStore.IsRole(token, "admin");
    }

    [HttpGet]
    public async Task<IActionResult> GetBooks()
    {
        if (!IsAdmin()) return Unauthorized();

        var books = await _db.Books.OrderBy(b => b.Id).ToListAsync();
        return Ok(books);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        if (!IsAdmin()) return Unauthorized();

        var totalBooks = await _db.Books.CountAsync();
        var totalStock = await _db.Books.SumAsync(b => (int?)b.Stock) ?? 0;
        var totalAmount = await _db.Books.SumAsync(b => (decimal?)(b.Price * b.Stock)) ?? 0m;

        return Ok(new
        {
            totalBooks,
            totalStock,
            totalAmount
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookRequest request)
    {
        if (!IsAdmin()) return Unauthorized();

        var book = new Book
        {
            Title = request.Title,
            Author = request.Author,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "未分类" : request.Category,
            Price = request.Price,
            Stock = request.Stock,
            Description = request.Description,
            CoverUrl = request.ImageUrl ?? string.Empty,
            ImageUrl = request.ImageUrl
        };

        _db.Books.Add(book);
        await _db.SaveChangesAsync();
        return Created($"/api/admin/books/{book.Id}", book);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBookRequest request)
    {
        if (!IsAdmin()) return Unauthorized();

        var book = await _db.Books.FindAsync(id);
        if (book is null)
        {
            return NotFound(new { message = "图书不存在" });
        }

        book.Title = request.Title;
        book.Author = request.Author;
        book.Category = string.IsNullOrWhiteSpace(request.Category) ? "未分类" : request.Category;
        book.Price = request.Price;
        book.Stock = request.Stock;
        book.Description = request.Description;
        book.CoverUrl = request.ImageUrl ?? string.Empty;
        book.ImageUrl = request.ImageUrl;

        await _db.SaveChangesAsync();
        return Ok(book);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdmin()) return Unauthorized();

        var book = await _db.Books.FindAsync(id);
        if (book is null)
        {
            return NotFound(new { message = "图书不存在" });
        }

        _db.Books.Remove(book);
        await _db.SaveChangesAsync();
        return Ok(new { message = "删除成功" });
    }
}
