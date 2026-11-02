using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // Tệp vượt giới hạn của API tải lên (RequestSizeLimit): báo như lỗi dung lượng ảnh thay vì lỗi hệ thống.
            _logger.LogWarning(ex, "Request body too large on {Method} {Path}", context.Request.Method, context.Request.Path);
            await Write(context, StatusCodes.Status413PayloadTooLarge, ApiResponses.Failure(ErrorCodes.AttachmentFileTooLarge,
                string.Format(Messages.AttachmentFileTooLarge,
                context.RequestServices.GetRequiredService<IOptions<AttachmentOptions>>().Value.MaxFileMb)));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict");
            await Write(context, StatusCodes.Status409Conflict,
                ApiResponses.Failure(ErrorCodes.CommonConcurrencyConflict, Messages.ConcurrencyConflict));
        }
        catch (DbUpdateException ex) when (MapSql(ex.InnerException as SqlException) is { } mapped)
        {
            _logger.LogWarning(ex, "Database rule violated: {Code}", mapped.Code);
            await Write(context, StatusCodes.Status409Conflict, ApiResponses.Failure(mapped.Code, mapped.Message));
        }
        catch (SqlException ex) when (MapSql(ex) is { } mapped)   // lỗi từ thủ tục (ExecuteSql)
        {
            _logger.LogWarning(ex, "Database rule violated: {Code}", mapped.Code);
            await Write(context, StatusCodes.Status409Conflict, ApiResponses.Failure(mapped.Code, mapped.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            await Write(context, StatusCodes.Status500InternalServerError,
                ApiResponses.Failure(ErrorCodes.CommonInternalError, Messages.InternalError));
        }
    }

    private static (string Code, string Message)? MapSql(SqlException? sql) => sql?.Number switch
    {
        2601 or 2627 => (ErrorCodes.CommonDuplicate, Messages.Duplicate),
        547 => (ErrorCodes.CommonConstraintViolation, Messages.ConstraintViolation),
        >= 50000 => (ErrorCodes.CommonDbRule, sql.Message),   // THROW 5xxxx trong trigger: câu tiếng Việt
        _ => null,
    };

    private static Task Write(HttpContext context, int status, ApiFailure body)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(body);
    }
}
