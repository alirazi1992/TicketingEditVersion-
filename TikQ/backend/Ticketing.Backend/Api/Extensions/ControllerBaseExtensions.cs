using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Ticketing.Backend.Api.Extensions;

/// <summary>
/// Returns 403 Forbidden with RFC 7807 ProblemDetails (title: "Forbidden", detail: message).
/// Use this instead of Forbid(message) — in ASP.NET Core, Forbid(string) treats the string as an auth scheme name, not an error message.
/// </summary>
public static class ControllerBaseExtensions
{
    public static IActionResult ForbiddenProblem(this ControllerBase controller, string detail)
    {
        return controller.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden",
            detail: detail);
    }
}
