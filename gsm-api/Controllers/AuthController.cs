using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GsmApi.Data;
using GsmApi.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GsmApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email и пароль обязательны.");
        }

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u =>
                u.Email == request.Email &&
                u.IsActive);

        if (user == null)
        {
            return Unauthorized("Неверный email или пароль.");
        }

        // ВАЖНО: сейчас у тебя пароли лежат в открытом виде.
        // Для прототипа сравниваем как есть:
        if (!string.Equals(user.PasswordHash, request.Password))
        {
            return Unauthorized("Неверный email или пароль.");
        }

        var token = GenerateJwtToken(user, out DateTime expiresAtUtc);

        var fullName = $"{user.FirstName} {user.LastName}".Trim();

        var response = new AuthResponseDto
        {
            UserId = user.UserId,
            Email = user.Email,
            FullName = string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName,
            Role = user.Role?.Name ?? "user",
            Token = token,
            ExpiresAtUtc = expiresAtUtc
        };

        return Ok(response);
    }

    // ---------- Генерация JWT ----------

    private string GenerateJwtToken(GsmApi.Models.User user, out DateTime expiresAtUtc)
    {
        var jwtSection = _config.GetSection("Jwt");
        var key = jwtSection.GetValue<string>("Key")!;
        var issuer = jwtSection.GetValue<string>("Issuer");
        var audience = jwtSection.GetValue<string>("Audience");
        var expiresMinutes = jwtSection.GetValue<int>("ExpiresMinutes", 120);

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var roleName = user.Role?.Name ?? "user";

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Role, roleName)
        };

        expiresAtUtc = DateTime.UtcNow.AddMinutes(expiresMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
