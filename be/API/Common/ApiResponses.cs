using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.API.Common;

public sealed record ApiSuccess<T>(bool Success, T? Data, string? Message);

public sealed record ApiFailure(bool Success, string ErrorCode, string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string[]>? Errors);

public static class ApiResponses
{
    public static ApiFailure Failure(string errorCode, string message, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(false, errorCode, message, errors);

    public static int StatusOf(ResultKind kind) => kind switch
    {
        ResultKind.Validation => StatusCodes.Status400BadRequest,
        ResultKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ResultKind.Forbidden => StatusCodes.Status403Forbidden,
        ResultKind.NotFound => StatusCodes.Status404NotFound,
        ResultKind.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status200OK
    };

    public static IActionResult ToResponse(this ControllerBase controller, Result result) =>
        result.IsSuccess
            ? new OkObjectResult(new ApiSuccess<object>(true, null, result.Message))
            : Fail(result);

    public static IActionResult ToResponse<T>(this ControllerBase controller, Result<T> result) =>
        result.IsSuccess
            ? new OkObjectResult(new ApiSuccess<T>(true, result.Data, result.Message))
            : Fail(result);

    /// <summary>Trả file Excel (TH-16) hoặc lỗi dạng ApiFailure như mọi endpoint khác.</summary>
    public static IActionResult ToExcelFile(this ControllerBase controller, Result<ExcelFileDto> result) =>
        result.IsSuccess
            ? controller.File(result.Data!.Content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Data.FileName)
            : Fail(result);

    private static IActionResult Fail(Result result) =>
        new ObjectResult(Failure(result.ErrorCode!, result.Message!, result.Errors)) { StatusCode = StatusOf(result.Kind) };
}
