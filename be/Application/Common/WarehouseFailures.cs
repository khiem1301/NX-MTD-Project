using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Common;

/// <summary>Các Failure hay dùng của luồng kho / cắt.</summary>
public static class WarehouseFailures
{
    public static Result<T> NotFound<T>() =>
        Result<T>.Failure(ErrorCodes.CommonNotFound, WarehouseMessages.NotFound, ResultKind.NotFound);

    public static Result NotFound() =>
        Result.Failure(ErrorCodes.CommonNotFound, WarehouseMessages.NotFound, ResultKind.NotFound);

    public static Result<T> InvalidStatus<T>() =>
        Result<T>.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.InvalidStatus);

    public static Result InvalidStatus() =>
        Result.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.InvalidStatus);

    public static Result<T> TeamMismatch<T>() =>
        Result<T>.Failure(ErrorCodes.WarehouseTeamMismatch, WarehouseMessages.TeamMismatch, ResultKind.Forbidden);

    public static Result TeamMismatch() =>
        Result.Failure(ErrorCodes.WarehouseTeamMismatch, WarehouseMessages.TeamMismatch, ResultKind.Forbidden);

    /// <summary>Đổi kiểu Failure (VD: Result của IStockService) sang Result&lt;T&gt; giữ nguyên mã lỗi.</summary>
    public static Result<T> From<T>(Result failure) =>
        Result<T>.Failure(failure.ErrorCode!, failure.Message!, failure.Kind, failure.Errors);
}
