using Microsoft.EntityFrameworkCore;
using NX_MTD.API.Common;
using NX_MTD.Application.Common;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next; _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { /* client bỏ request */ }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict");
            await Write(context, StatusCodes.Status409Conflict,
                ApiResponses.Failure(ErrorCodes.CommonConcurrencyConflict, Messages.ConcurrencyConflict));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            await Write(context, StatusCodes.Status500InternalServerError,
                ApiResponses.Failure(ErrorCodes.CommonInternalError, Messages.InternalError));
        }
    }

    private static Task Write(HttpContext context, int status, ApiFailure body)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(body);
    }
}
