using BookStoreApi.Data;
using BookStoreApi.Models;
using BookStoreApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStoreApi.Controllers;

[ApiController]
[Route("api/reader/orders")]
public class ReaderOrdersController : ControllerBase
{
    private readonly BookStoreDbContext _db;
    private readonly AuthTokenStore _tokenStore;

    public ReaderOrdersController(BookStoreDbContext db, AuthTokenStore tokenStore)
    {
        _db = db;
        _tokenStore = tokenStore;
    }

    private AuthSession? GetReaderSession()
    {
        var token = Request.Headers["X-Auth-Token"].ToString();
        var session = _tokenStore.GetSession(token);
        if (session is null)
        {
            return null;
        }

        return string.Equals(session.Role, "reader", StringComparison.OrdinalIgnoreCase)
            ? session
            : null;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyOrders()
    {
        var session = GetReaderSession();
        if (session is null) return Unauthorized();

        var orders = await _db.Orders
            .Where(o => o.UserId == session.UserId)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Book)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new
            {
                o.Id,
                o.OrderDate,
                o.Status,
                o.TotalAmount,
                o.ShippingAddress,
                o.ShippingPhone,
                o.ShippedAt,
                o.ReceivedAt,
                items = o.OrderItems.Select(i => new
                {
                    i.BookId,
                    title = i.Book!.Title,
                    i.Quantity,
                    i.Price,
                    lineTotal = i.Price * i.Quantity
                })
            })
            .ToListAsync();

        return Ok(orders);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteMyOrder(int id)
    {
        var session = GetReaderSession();
        if (session is null) return Unauthorized();

        var order = await _db.Orders
            .Where(o => o.Id == id && o.UserId == session.UserId)
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync();

        if (order is null)
        {
            return NotFound(new { message = "订单不存在" });
        }

        if (order.Status != OrderStatus.Unshipped)
        {
            return BadRequest(new { message = "仅未发货订单可以删除" });
        }

        var bookIds = order.OrderItems.Select(i => i.BookId).Distinct().ToList();
        var books = await _db.Books.Where(b => bookIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id);

        await using var tx = await _db.Database.BeginTransactionAsync();

        foreach (var item in order.OrderItems)
        {
            if (books.TryGetValue(item.BookId, out var book))
            {
                book.Stock += item.Quantity;
            }
        }

        _db.OrderItems.RemoveRange(order.OrderItems);
        _db.Orders.Remove(order);

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { message = "订单记录已删除" });
    }

    [HttpPatch("{id:int}/receive")]
    public async Task<IActionResult> ReceiveMyOrder(int id)
    {
        var session = GetReaderSession();
        if (session is null) return Unauthorized();

        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == session.UserId);

        if (order is null)
        {
            return NotFound(new { message = "订单不存在" });
        }

        if (order.Status == OrderStatus.Received)
        {
            return BadRequest(new { message = "该订单已确认收货" });
        }

        if (order.Status != OrderStatus.Shipped)
        {
            return BadRequest(new { message = "只有已发货订单才可确认收货" });
        }

        order.Status = OrderStatus.Received;
        order.ReceivedAt = DateTime.Now;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "确认收货成功",
            orderId = order.Id,
            status = order.Status,
            receivedAt = order.ReceivedAt
        });
    }
}
