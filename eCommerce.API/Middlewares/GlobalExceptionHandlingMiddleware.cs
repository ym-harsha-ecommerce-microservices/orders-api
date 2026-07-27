using System.Net;
using System.Text.Json;
using eCommerce.BLL.Exceptions;
using MongoDB.Driver;

namespace eCommerce.API.Middlewares;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (BadRequestException ex)
        {
            _logger.LogWarning(ex, "Bad request: {Message}", ex.Message);
            await WriteErrorResponse(httpContext, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found: {Message}", ex.Message);
            await WriteErrorResponse(httpContext, HttpStatusCode.NotFound, ex.Message);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            _logger.LogWarning(ex, "Duplicate key error: {Message}", ex.Message);
            await WriteErrorResponse(httpContext, HttpStatusCode.Conflict, "A record with this identifier already exists.");
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Database error occurred: {Message}", ex.Message);
            await WriteErrorResponse(httpContext, HttpStatusCode.ServiceUnavailable, "A database error occurred. Please try again later.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred: {Message}", ex.Message);
            var detail = httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment()
                ? ex.ToString()
                : null;
            await WriteErrorResponse(httpContext, HttpStatusCode.InternalServerError,
                "An unexpected error occurred. Please try again later.", detail);
        }
    }

    private static async Task WriteErrorResponse(HttpContext httpContext, HttpStatusCode statusCode, string message, string? detail = null)
    {
        httpContext.Response.ContentType = "application/json";
        httpContext.Response.StatusCode = (int)statusCode;

        var errorResponse = new
        {
            StatusCode = (int)statusCode,
            Message = message,
            Detail = detail
        };

        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
    }
}

public static class GlobalExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }
}