using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ticketing.Backend.Api.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Unhandled exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                ex.GetType().Name, ex.Message, ex.StackTrace);
            
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var code = HttpStatusCode.InternalServerError;
        var message = "An error occurred while processing your request.";
        var details = exception.Message;

        // Determine status code based on exception type
        // UnauthorizedAccessException = permission denied (403), not auth failure (401)
        switch (exception)
        {
            case UnauthorizedAccessException uex:
                code = HttpStatusCode.Forbidden;
                message = "Forbidden";
                details = uex.Message;
                break;
            case ArgumentException:
                code = HttpStatusCode.BadRequest;
                message = "Invalid request parameters.";
                break;
            case KeyNotFoundException:
            case InvalidOperationException when exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase):
                code = HttpStatusCode.NotFound;
                message = "Resource not found.";
                break;
        }

        var isDevelopment = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();

        var response = new
        {
            status = (int)code,
            title = message,
            detail = isDevelopment ? details : message,
            type = exception.GetType().Name,
            traceId = context.TraceIdentifier
        };

        if (isDevelopment)
        {
            // Include stack trace in development
            var responseWithStackTrace = new
            {
                status = (int)code,
                title = message,
                detail = details,
                type = exception.GetType().Name,
                traceId = context.TraceIdentifier,
                stackTrace = exception.StackTrace,
                innerException = exception.InnerException != null ? new
                {
                    message = exception.InnerException.Message,
                    type = exception.InnerException.GetType().Name,
                    stackTrace = exception.InnerException.StackTrace
                } : null
            };
            
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)code;
            return context.Response.WriteAsync(JsonSerializer.Serialize(responseWithStackTrace, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)code;
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));
    }
}

