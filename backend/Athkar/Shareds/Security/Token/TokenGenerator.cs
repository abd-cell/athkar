using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Athkar.Shareds.Enums;

namespace Athkar.Shareds.Security.Token;

public class TokenGenerator : ITokenGenerator
{
    private readonly JwtSettings settings;

    public TokenGenerator(IOptions<JwtSettings> options) => settings = options.Value;

    public DateTime AccessExpiry => DateTime.UtcNow.AddMinutes(settings.AccessMinutes);
    public DateTime RefreshExpiry => DateTime.UtcNow.AddDays(settings.RefreshDays);

    public string CreateAccessToken(int userId, string sessionKey, IEnumerable<Roles> roles)
    {
        var claims = new List<Claim>
        {
            new(AppClaims.UserId, userId.ToString()),
            new(AppClaims.SessionKey, sessionKey),
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, sessionKey),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r.ToString())));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret));
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: AccessExpiry,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}
