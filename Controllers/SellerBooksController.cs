using BookStoreApi.Contracts;
using BookStoreApi.Data;
using BookStoreApi.Models;
using BookStoreApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStoreApi.Controllers;

[ApiController]
[Route("api/seller/books")]
public class SellerBooksController : ControllerBase
{
    private readonly BookStoreDbContext _db;
    private readonly AuthTokenStore _tokenStore;

    public SellerBooksController(BookStoreDbContext db, AuthTokenStore tokenStore)
    {
        _db = db;
        _tokenStore = tokenStore;
    }

    private bool IsSeller()
    {
        var token = Request.Headers["X-Auth-Token"].ToString();
        return _tokenStore.IsRole(token, "seller");
    }

    [HttpGet]
    public async Task<IActionResult> GetBooks()
    {
        if (!IsSeller()) return Unauthorized();

        var books = await _db.Books
            .Where(b => b.IsListed)
            .OrderBy(b => b.Id)
            .ToListAsync();
        return Ok(books);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        if (!IsSeller()) return Unauthorized();

        var totalBooks = await _db.Books.CountAsync(b => b.IsListed);
        var totalStock = await _db.Books
            .Where(b => b.IsListed)
            .SumAsync(b => (int?)b.Stock) ?? 0;

        return Ok(new
        {
            totalBooks,
            totalStock
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookRequest request)
    {
        if (!IsSeller()) return Unauthorized();

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
        return Created($"/api/seller/books/{book.Id}", book);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBookRequest request)
    {
        if (!IsSeller()) return Unauthorized();

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
        book.IsListed = true;

        await _db.SaveChangesAsync();
        return Ok(book);
    }

    [HttpGet("{id:int}/delete-impact")]
    public async Task<IActionResult> GetDeleteImpact(int id)
    {
        if (!IsSeller()) return Unauthorized();

        var book = await _db.Books
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);
        if (book is null)
        {
            return NotFound(new { message = "图书不存在或已被删除" });
        }

        var cartItemCount = await _db.Carts.CountAsync(c => c.BookId == id);
        var orderItemCount = await _db.OrderItems.CountAsync(oi => oi.BookId == id);

        return Ok(new
        {
            bookId = id,
            title = book.Title,
            cartItemCount,
            orderItemCount
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsSeller()) return Unauthorized();

        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == id);
        if (book is null)
        {
            return NotFound(new { message = "图书不存在或已被删除" });
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var removedCartItems = await _db.Carts
            .Where(c => c.BookId == id)
            .ExecuteDeleteAsync();

        var removedOrderItems = await _db.OrderItems
            .Where(oi => oi.BookId == id)
            .ExecuteDeleteAsync();

        var affectedRows = await _db.Books
            .Where(b => b.Id == id)
            .ExecuteDeleteAsync();

        if (affectedRows == 0)
        {
            await tx.RollbackAsync();
            return NotFound(new { message = "图书不存在或已被删除" });
        }

        var stillExists = await _db.Books.AnyAsync(b => b.Id == id);
        if (stillExists)
        {
            await tx.RollbackAsync();
            return StatusCode(500, new { message = "删除校验失败，请重试" });
        }

        await tx.CommitAsync();

        return Ok(new
        {
            message = "删除成功，图书已从数据库彻底移除",
            deletedBookId = id,
            affectedRows,
            removedCartItems,
            removedOrderItems,
            verifyDeleted = true,
            hardDeleted = true
        });
    }
}
