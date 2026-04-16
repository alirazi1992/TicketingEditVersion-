namespace Ticketing.Backend.Infrastructure.Auth;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "TicketingBackend";
    public string Audience { get; set; } = "TicketingFrontend";
    public int ExpirationMinutes { get; set; } = 120;
}
