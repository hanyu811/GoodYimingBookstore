using BookStoreApi.Contracts;
using BookStoreApi.Data;
using BookStoreApi.Models;
using BookStoreApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStoreApi.Controllers;

[ApiController]
[Route("api/reader/cart")]
public class ReaderCartController : ControllerBase
{
    private readonly BookStoreDbContext _db;
    private readonly AuthTokenStore _tokenStore;

    public ReaderCartController(BookStoreDbContext db, AuthTokenStore tokenStore)
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
    public async Task<IActionResult> GetCart()
    {
        var session = GetReaderSession();
        if (session is null) return Unauthorized();

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == session.UserId)
            .Select(u => new { u.Address, u.PhoneNumber })
            .FirstOrDefaultAsync();

        var readerUserName = session.UserName;

        var items = await _db.Carts
            .Where(c => c.ReaderUserName == readerUserName)
            .Include(c => c.Book)
            .OrderBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.ReaderUserName,
                c.BookId,
                c.Quantity,
                bookTitle = c.Book!.Title,
                price = c.Book!.Price,
                imageUrl = c.Book!.ImageUrl,
                lineTotal = c.Book!.Price * c.Quantity,
                stock = c.Book!.Stock
            })
            .ToListAsync();

        return Ok(new
        {
            items,
            totalAmount = items.Sum(i => i.lineTotal),
            defaultShippingAddress = user?.Address ?? string.Empty,
            defaultShippingPhone = user?.PhoneNumber ?? string.Empty
        });
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddCartItemRequest request)
    {
        var session = GetReaderSession();
        if (session is null) return Unauthorized();

        if (request.Quantity <= 0)
        {
            return BadRequest(new { message = "数量必须大于0" });
        }

        var book = await _db.Books.FindAsync(request.BookId);
        if (book is null)
        {
            return NotFound(new { message = "图书不存在" });
        }

        if (!book.IsListed)
        {
            return BadRequest(new { message = "该图书已下架，暂不可购买" });
        }

        if (book.Stock < request.Quantity)
        {
            return BadRequest(new { message = "库存不足" });
        }

        var existing = await _db.Carts.FirstOrDefaultAsync(c => c.ReaderUserName == session.UserName && c.BookId == request.BookId);
        if (existing is null)
        {
            _db.Carts.Add(new Cart
            {
                UserId = session.UserId,
                ReaderUserName = session.UserName,
                BookId = request.BookId,
                Quantity = request.Quantity
            });
        }
        else
        {
            var newQty = existing.Quantity + request.Quantity;
            if (book.Stock < newQty)
            {
                return BadRequest(new { message = "加入后超过库存" });
            }
            existing.Quantity = newQty;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "已加入购物车" });
    }

    [HttpPut("items/{id:int}")]
    public async Task<IActionResult> UpdateItem(int id, [FromBody] UpdateCartItemRequest request)
    {
        var session = GetReaderSession();
        if (session is null) return Unauthorized();

        var cart = await _db.Carts.Include(c => c.Book).FirstOrDefaultAsync(c => c.Id == id && c.ReaderUserName == session.UserName);
        if (cart is null)
        {
            return NotFound(new { message = "购物车项不存在" });
        }

        if (request.Quantity <= 0)
        {
            _db.Carts.Remove(cart);
            await _db.SaveChangesAsync();
            return Ok(new { message = "已移除" });
        }

        if (cart.Book is null)
        {
            return BadRequest(new { message = "图书不存在" });
        }

        if (!cart.Book.IsListed)
        {
            return BadRequest(new { message = "该图书已下架，无法修改数量" });
        }

        if (cart.Book.Stock < request.Quantity)
        {
            return BadRequest(new { message = "库存不足" });
        }

        cart.Quantity = request.Quantity;
        await _db.SaveChangesAsync();
        return Ok(new { message = "更新成功" });
    }

    [HttpDelete("items/{id:int}")]
    public async Task<IActionResult> RemoveItem(int id)
    {
        var session = GetReaderSession();
        if (session is null) return Unauthorized();

        var cart = await _db.Carts.FirstOrDefaultAsync(c => c.Id == id && c.ReaderUserName == session.UserName);
        if (cart is null)
        {
            return NotFound(new { message = "购物车项不存在" });
        }

        _db.Carts.Remove(cart);
        await _db.SaveChangesAsync();
        return Ok(new { message = "删除成功" });
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
    {
        var session = GetReaderSession();
        if (session is null) return Unauthorized();

        var shippingAddress = (request.ShippingAddress ?? string.Empty).Trim();
        var shippingPhone = (request.ShippingPhone ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(shippingAddress) || string.IsNullOrWhiteSpace(shippingPhone))
        {
            return BadRequest(new { message = "收货地址和联系电话不能为空" });
        }

        if (shippingAddress.Length > 200)
        {
            return BadRequest(new { message = "收货地址长度不能超过 200" });
        }

        if (!IsLikelyPhoneNumber(shippingPhone))
        {
            return BadRequest(new { message = "联系电话格式不正确" });
        }

        var cartItems = await _db.Carts
            .Where(c => c.ReaderUserName == session.UserName)
            .Include(c => c.Book)
            .ToListAsync();

        if (!cartItems.Any())
        {
            return BadRequest(new { message = "购物车为空" });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == session.UserId);
        if (user is null)
        {
            return Unauthorized(new { message = "用户信息不存在，请重新登录" });
        }

        foreach (var item in cartItems)
        {
            var book = item.Book;
            if (book is null)
            {
                return BadRequest(new { message = "图书不存在" });
            }

            if (!book.IsListed)
            {
                return BadRequest(new { message = $"图书已下架：{book.Title}" });
            }

            if (book.Stock < item.Quantity)
            {
                return BadRequest(new { message = $"库存不足：{book.Title}" });
            }
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        var order = new Order
        {
            UserId = session.UserId,
            Status = OrderStatus.Unshipped,
            TotalAmount = cartItems.Sum(i => i.Book!.Price * i.Quantity),
            OrderDate = DateTime.Now,
            ShippingAddress = shippingAddress,
            ShippingPhone = shippingPhone
        };

        user.Address = shippingAddress;
        user.PhoneNumber = shippingPhone;

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var orderItems = new List<OrderItem>();
        foreach (var item in cartItems)
        {
            item.Book!.Stock -= item.Quantity;
            orderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                BookId = item.BookId,
                Quantity = item.Quantity,
                Price = item.Book.Price
            });
        }

        _db.OrderItems.AddRange(orderItems);
        _db.Carts.RemoveRange(cartItems);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            message = "结算成功",
            orderId = order.Id,
            totalAmount = order.TotalAmount
        });
    }

    private static bool IsLikelyPhoneNumber(string value)
    {
        if (value.Length < 6 || value.Length > 20)
        {
            return false;
        }

        return value.All(ch => char.IsDigit(ch) || ch is '+' or '-' or ' ' or '(' or ')');
    }
}
