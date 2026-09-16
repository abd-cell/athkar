namespace Athkar.Shareds.Security.Token;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Athkar";
    public string Audience { get; set; } = "Athkar";

    /// <summary>Access-token lifetime. Short on purpose; the refresh token carries the session.</summary>
    public int AccessMinutes { get; set; } = 60;

    public int RefreshDays { get; set; } = 30;
}
