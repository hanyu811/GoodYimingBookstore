using BookStoreApi.Data;
using BookStoreApi.Models;
using BookStoreApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStoreApi.Controllers;

[ApiController]
[Route("api/seller/orders")]
public class SellerOrdersController : ControllerBase
{
    private readonly BookStoreDbContext _db;
    private readonly AuthTokenStore _tokenStore;

    public SellerOrdersController(BookStoreDbContext db, AuthTokenStore tokenStore)
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
    public async Task<IActionResult> GetOrders()
    {
        if (!IsSeller()) return Unauthorized();

        var orders = await _db.Orders
            .Include(o => o.User)
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
                buyer = new
                {
                    id = o.UserId,
                    userName = o.User!.UserName,
                    displayName = o.User!.DisplayName
                },
                reader = new
                {
                    id = o.UserId,
                    userName = o.User!.UserName,
                    displayName = o.User!.DisplayName
                },
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

    [HttpPatch("{id:int}/ship")]
    public async Task<IActionResult> ShipOrder(int id)
    {
        if (!IsSeller()) return Unauthorized();

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
        {
            return NotFound(new { message = "订单不存在" });
        }

        if (order.Status == OrderStatus.Received)
        {
            return BadRequest(new { message = "订单已收货，不能再次发货" });
        }

        if (order.Status == OrderStatus.Shipped)
        {
            return BadRequest(new { message = "订单已发货，请勿重复操作" });
        }

        order.Status = OrderStatus.Shipped;
        order.ShippedAt = DateTime.Now;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "发货成功",
            orderId = order.Id,
            status = order.Status,
            shippedAt = order.ShippedAt
        });
    }
}
