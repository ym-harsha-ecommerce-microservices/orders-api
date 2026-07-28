using System.Net;
using System.Text.Json;
using eCommerce.BLL.Exceptions;
using MongoDB.Driver;

namespace eCommerce.API.Middlewares;

/// <summary>
/// Middleware that catches exceptions thrown anywhere in the request pipeline
/// and translates them into standardized JSON error responses with appropriate
/// HTTP status codes, based on the exception type.
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="GlobalExceptionHandlingMiddleware"/>.
    /// </summary>
    /// <param name="next">The next middleware delegate in the pipeline.</param>
    /// <param name="logger">Logger used to record caught exceptions.</param>
    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the next middleware in the pipeline, catching and handling known
    /// exception types (<see cref="BadRequestException"/>, <see cref="NotFoundException"/>,
    /// MongoDB duplicate-key errors, other <see cref="MongoException"/>s) with specific
    /// status codes, and falling back to a generic 500 response for anything else.
    /// </summary>
    /// <param name="httpContext">The current HTTP context for the request.</param>
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

    /// <summary>
    /// Writes a standardized JSON error response to the HTTP response body.
    /// </summary>
    /// <param name="httpContext">The current HTTP context to write the response to.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="message">A user-facing error message.</param>
    /// <param name="detail">Optional additional detail, typically only populated in development environments.</param>
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

/// <summary>
/// Provides an extension method for registering <see cref="GlobalExceptionHandlingMiddleware"/>
/// into the application's request pipeline.
/// </summary>
public static class GlobalExceptionHandlingMiddlewareExtensions
{
    /// <summary>
    /// Adds <see cref="GlobalExceptionHandlingMiddleware"/> to the application's request pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> instance, for chaining.</returns>
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }
}