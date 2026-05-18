using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Woola.PhotoManager.Shared.Configuration;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtOptions _jwt;
    private readonly AuthOptions _auth;

    public AuthController(IOptions<JwtOptions> jwt, IOptions<AuthOptions> auth)
    {
        _jwt = jwt.Value;
        _auth = auth.Value;
    }

    [HttpPost("login")]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        if (request.Username != _auth.DefaultUsername || request.Password != _auth.DefaultPassword)
            return Unauthorized(new ErrorResponse { Type = "Unauthorized", Title = "Invalid credentials", Status = 401, Detail = "The username or password is incorrect." });
        var token = GenerateJwt(request.Username);
        return Ok(new LoginResponse { AccessToken = token, RefreshToken = token, ExpiresIn = (int)TimeSpan.FromHours(_jwt.ExpiryHours).TotalSeconds, User = new UserDto { Id = 1, Username = request.Username, DisplayName = "Administrator", Role = "Admin" } });
    }

    [HttpPost("logout")]
    public ActionResult Logout()
    {
        return Ok(new { status = "logged_out" });
    }

    private string GenerateJwt(string username)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer: _jwt.Issuer, audience: _jwt.Audience,
            claims: new[] { new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, "Admin") },
            expires: DateTime.UtcNow.AddHours(_jwt.ExpiryHours), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
