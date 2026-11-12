using BookStoreApi.Contracts;
using BookStoreApi.Data;
using BookStoreApi.Models;
using BookStoreApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStoreApi.Controllers;

[ApiController]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly BookStoreDbContext _db;
    private readonly AuthTokenStore _tokenStore;

    public AdminUsersController(BookStoreDbContext db, AuthTokenStore tokenStore)
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
    public async Task<IActionResult> GetUsers()
    {
        if (!IsAdmin()) return Unauthorized();

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

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!IsAdmin()) return Unauthorized();

        var role = NormalizeRole(request.Role);
        if (role is null)
        {
            return BadRequest(new { message = "角色必须是 reader/seller/admin" });
        }

        var userName = (request.UserName ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new { message = "用户名和密码不能为空" });
        }

        var exists = await _db.Users.AnyAsync(u => u.UserName == userName);
        if (exists)
        {
            return Conflict(new { message = "用户名已存在" });
        }

        var user = new User
        {
            UserName = userName,
            PasswordHash = password,
            Role = role,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? userName : request.DisplayName!.Trim()
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            user.Id,
            user.UserName,
            user.Role,
            user.DisplayName
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        if (!IsAdmin()) return Unauthorized();

        var user = await _db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound(new { message = "用户不存在" });
        }

        var role = NormalizeRole(request.Role);
        if (role is null)
        {
            return BadRequest(new { message = "角色必须是 reader/seller/admin" });
        }

        user.Role = role;
        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            user.DisplayName = request.DisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = request.Password;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            user.Id,
            user.UserName,
            user.Role,
            user.DisplayName
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (!IsAdmin()) return Unauthorized();

        var selfToken = Request.Headers["X-Auth-Token"].ToString();
        var current = _tokenStore.GetSession(selfToken);
        if (current is not null && current.UserId == id)
        {
            return BadRequest(new { message = "不能删除当前登录管理员账号" });
        }

        var user = await _db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound(new { message = "用户不存在" });
        }

        var hasOrder = await _db.Orders.AnyAsync(o => o.UserId == id);
        var hasCart = await _db.Carts.AnyAsync(c => c.UserId == id);
        if (hasOrder || hasCart)
        {
            return BadRequest(new { message = "该用户已有购物车或订单数据，无法删除" });
        }

        var affectedRows = await _db.Users
            .Where(u => u.Id == id)
            .ExecuteDeleteAsync();

        if (affectedRows == 0)
        {
            return NotFound(new { message = "用户不存在或已被删除" });
        }

        var stillExists = await _db.Users.AnyAsync(u => u.Id == id);
        if (stillExists)
        {
            return StatusCode(500, new { message = "删除校验失败，请重试" });
        }

        return Ok(new
        {
            message = "删除成功",
            deletedUserId = id,
            affectedRows,
            verifyDeleted = true
        });
    }

    private static string? NormalizeRole(string? role)
    {
        var normalized = (role ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "reader" or "seller" or "admin" ? normalized : null;
    }
}
