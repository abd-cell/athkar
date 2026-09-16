using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;

namespace Athkar.Shareds.Security.Token;

[SingletonInjectable]
public interface ITokenGenerator
{
    /// <summary>Signs an access token carrying the user id, roles and session key.</summary>
    string CreateAccessToken(int userId, string sessionKey, IEnumerable<Roles> roles);

    /// <summary>A refresh token is opaque randomness — it is looked up, never parsed.</summary>
    string CreateRefreshToken();

    DateTime AccessExpiry { get; }
    DateTime RefreshExpiry { get; }
}
