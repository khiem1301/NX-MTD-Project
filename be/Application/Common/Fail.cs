using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Common;

/// <summary>Lỗi hay gặp, để service không lặp lại.</summary>
public static class Fail
{
    public static Result NotFound() => Result.Failure(ErrorCodes.CommonNotFound, Messages.NotFound, ResultKind.NotFound);
    public static Result<T> NotFound<T>() => Result<T>.Failure(ErrorCodes.CommonNotFound, Messages.NotFound, ResultKind.NotFound);

    /// <summary>Chuyển Result thất bại sang Result&lt;T&gt; giữ nguyên mã lỗi.</summary>
    public static Result<T> As<T>(Result failed) =>
        Result<T>.Failure(failed.ErrorCode!, failed.Message!, failed.Kind, failed.Errors);
}
