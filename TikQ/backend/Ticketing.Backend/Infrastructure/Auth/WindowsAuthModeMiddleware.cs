using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Ticketing.Backend.Infrastructure.Auth;

/// <summary>
/// Enforces WindowsAuth mode: Off (block Windows endpoint with 403), Enforce (require auth on non-allowlisted routes).
/// Optional mode has no middleware behavior; /api/auth/windows returns 401 with WWW-Authenticate when no Windows identity.
/// </summary>
public class WindowsAuthModeMiddleware
{
    private static readonly PathString ApiAuthPrefix = new("/api/auth");
    private static readonly PathString ApiHealthPrefix = new("/api/health");
    private static readonly PathString HealthPath = new("/health");
    private static readonly PathString WindowsPath = new("/api/auth/windows");

    private readonly RequestDelegate _next;
    private readonly WindowsAuthOptions _options;

    public WindowsAuthModeMiddleware(RequestDelegate next, IOptions<WindowsAuthOptions> options)
    {
        _next = next;
        _options = options?.Value ?? new WindowsAuthOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var mode = _options.EffectiveMode;

        // Off: block Windows-specific endpoint with 403 and clear message
        if (string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase))
        {
            if (path.Value?.Equals(WindowsPath.Value, StringComparison.OrdinalIgnoreCase) == true)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "WINDOWS_AUTH_DISABLED",
                    message = "Windows authentication is disabled. Use email/password login or set WindowsAuth:Mode to Optional or Enforce."
                });
                return;
            }
            await _next(context);
            return;
        }

        // Enforce: require authentication for all routes except allowlist
        if (string.Equals(mode, "Enforce", StringComparison.OrdinalIgnoreCase))
        {
            if (IsAllowlisted(path))
            {
                await _next(context);
                return;
            }
            if (context.User?.Identity?.IsAuthenticated != true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.Append("WWW-Authenticate", "Bearer");
                if (_options.IsWindowsAuthAvailable)
                    context.Response.Headers.Append("WWW-Authenticate", "Negotiate");
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "AUTH_REQUIRED",
                    message = "Authentication required. Use /api/auth/login (email/password) or Windows Integrated Authentication."
                });
                return;
            }
        }

        await _next(context);
    }

    private static bool IsAllowlisted(PathString path)
    {
        if (path.StartsWithSegments(ApiHealthPrefix, StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.Equals(HealthPath, StringComparison.OrdinalIgnoreCase))
            return true;
        if (path.StartsWithSegments(ApiAuthPrefix, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
