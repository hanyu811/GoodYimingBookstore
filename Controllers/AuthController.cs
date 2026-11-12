using BookStoreApi.Contracts;
using BookStoreApi.Data;
using BookStoreApi.Models;
using BookStoreApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookStoreApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly BookStoreDbContext _db;
    private readonly AuthTokenStore _tokenStore;

    public AuthController(BookStoreDbContext db, AuthTokenStore tokenStore)
    {
        _db = db;
        _tokenStore = tokenStore;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var normalizedRole = (request.Role ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedRole is not ("reader" or "seller" or "admin"))
        {
            return BadRequest(new { message = "非法身份类型，仅支持 reader/seller/admin" });
        }

        var userName = (request.UserName ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new { message = "用户名和密码不能为空" });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.UserName == userName &&
            u.Role == normalizedRole &&
            u.PasswordHash == password);

        if (user is null)
        {
            return Unauthorized(new { message = "账号、密码或身份不匹配" });
        }

        var token = _tokenStore.CreateToken(user.Id, user.UserName, user.Role);
        var redirectUrl = user.Role switch
        {
            "admin" => "/admin.html",
            "seller" => "/seller.html",
            _ => "/store.html"
        };

        return Ok(new
        {
            role = user.Role,
            displayName = user.DisplayName,
            address = user.Address,
            phoneNumber = user.PhoneNumber,
            token,
            redirectUrl
        });
    }

    [HttpPost("register-reader")]
    public async Task<IActionResult> RegisterReader([FromBody] RegisterReaderRequest request)
    {
        var userName = (request.UserName ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;
        var address = (request.Address ?? string.Empty).Trim();
        var phoneNumber = (request.PhoneNumber ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new { message = "用户名和密码不能为空" });
        }

        if (password.Length < 6)
        {
            return BadRequest(new { message = "密码长度至少为 6 位" });
        }

        var normalizedUserName = userName.ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.UserName.ToLower() == normalizedUserName);
        if (exists)
        {
            return Conflict(new { message = "用户名已存在" });
        }

        if (address.Length > 200)
        {
            return BadRequest(new { message = "地址长度不能超过 200" });
        }

        if (!string.IsNullOrWhiteSpace(phoneNumber) && !IsLikelyPhoneNumber(phoneNumber))
        {
            return BadRequest(new { message = "手机号格式不正确" });
        }

        var user = new User
        {
            UserName = userName,
            PasswordHash = password,
            Role = "reader",
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? userName : request.DisplayName!.Trim(),
            Address = string.IsNullOrWhiteSpace(address) ? null : address,
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "读者注册成功",
            user = new
            {
                user.Id,
                user.UserName,
                user.Role,
                user.DisplayName,
                user.Address,
                user.PhoneNumber
            }
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
