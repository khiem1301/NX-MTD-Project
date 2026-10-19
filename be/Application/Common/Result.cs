namespace NX_MTD.Application.Common;

public enum ResultKind { Ok, Validation, Unauthorized, Forbidden, NotFound, Conflict }

public class Result
{
    protected Result(bool isSuccess, ResultKind kind, string? errorCode, string? message,
        IReadOnlyDictionary<string, string[]>? errors)
    {
        IsSuccess = isSuccess; Kind = kind; ErrorCode = errorCode; Message = message; Errors = errors;
    }

    public bool IsSuccess { get; }
    public ResultKind Kind { get; }
    public string? ErrorCode { get; }
    public string? Message { get; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public static Result Success(string? message = null) => new(true, ResultKind.Ok, null, message, null);

    /// <summary>Mặc định Conflict (409) cho lỗi nghiệp vụ.</summary>
    public static Result Failure(string errorCode, string message, ResultKind kind = ResultKind.Conflict,
        IReadOnlyDictionary<string, string[]>? errors = null) => new(false, kind, errorCode, message, errors);
}

public sealed class Result<T> : Result
{
    private Result(bool isSuccess, ResultKind kind, T? data, string? errorCode, string? message,
        IReadOnlyDictionary<string, string[]>? errors) : base(isSuccess, kind, errorCode, message, errors)
    {
        Data = data;
    }

    public T? Data { get; }

    public static Result<T> Success(T data, string? message = null) => new(true, ResultKind.Ok, data, null, message, null);

    public new static Result<T> Failure(string errorCode, string message, ResultKind kind = ResultKind.Conflict,
        IReadOnlyDictionary<string, string[]>? errors = null) => new(false, kind, default, errorCode, message, errors);
}
