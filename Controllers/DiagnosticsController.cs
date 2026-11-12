using BookStoreApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStoreApi.Controllers;

[ApiController]
[Route("api/diagnostics")]
public class DiagnosticsController : ControllerBase
{
    private readonly BookStoreDbContext _db;

    public DiagnosticsController(BookStoreDbContext db)
    {
        _db = db;
    }

    [HttpGet("db")]
    public async Task<IActionResult> GetDbInfo()
    {
        var info = await _db.Database.SqlQueryRaw<DbInfoRow>(@"
SELECT
    @@SERVERNAME AS ServerName,
    DB_NAME() AS DatabaseName,
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE') AS TableCount,
    (SELECT COUNT(*) FROM dbo.Books) AS BookCount,
    (SELECT COUNT(*) FROM dbo.Users) AS UserCount,
    (SELECT COUNT(*) FROM dbo.Carts) AS CartCount,
    (SELECT COUNT(*) FROM dbo.Orders) AS OrderCount,
    (SELECT COUNT(*) FROM dbo.OrderItems) AS OrderItemCount").FirstAsync();

        return Ok(new
        {
            server = info.ServerName,
            database = info.DatabaseName,
            tableCount = info.TableCount,
            bookCount = info.BookCount,
            userCount = info.UserCount,
            cartCount = info.CartCount,
            orderCount = info.OrderCount,
            orderItemCount = info.OrderItemCount
        });
    }

    [HttpPost("seed")]
    public IActionResult SeedNow()
    {
        DbInitializer.EnsureSeedData(_db);
        return Ok(new { message = "seed completed" });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsersSnapshot()
    {
        var users = await _db.Users
            .OrderBy(u => u.Id)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Role,
                u.DisplayName
            })
            .ToListAsync();

        return Ok(new
        {
            count = users.Count,
            users
        });
    }

    [HttpGet("books")]
    public async Task<IActionResult> GetBooksSnapshot()
    {
        var books = await _db.Books
            .OrderBy(b => b.Id)
            .Select(b => new
            {
                b.Id,
                b.Title,
                b.Stock,
                b.Price
            })
            .ToListAsync();

        return Ok(new
        {
            count = books.Count,
            books
        });
    }

    [HttpGet("books/{id:int}")]
    public async Task<IActionResult> GetBookExists(int id)
    {
        var exists = await _db.Books.AnyAsync(b => b.Id == id);
        return Ok(new
        {
            id,
            exists
        });
    }

    private class DbInfoRow
    {
        public string ServerName { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public int TableCount { get; set; }
        public int BookCount { get; set; }
        public int UserCount { get; set; }
        public int CartCount { get; set; }
        public int OrderCount { get; set; }
        public int OrderItemCount { get; set; }
    }
}
