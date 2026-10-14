namespace NX_MTD.Application.Common
{
    public class Result
    {
        public bool IsSuccess { get; protected set; }
        public string? Message { get; protected set; }
        public List<string> Errors { get; protected set; } = new();

        public static Result Success(string? message = null)
            => new() { IsSuccess = true, Message = message };

        public static Result Failure(string message, List<string>? errors = null)
            => new() { IsSuccess = false, Message = message, Errors = errors ?? new() };
    }

    public class Result<T> : Result
    {
        public T? Data { get; private set; }

        public static Result<T> Success(T data, string? message = null)
            => new() { IsSuccess = true, Data = data, Message = message };

        public static new Result<T> Failure(string message, List<string>? errors = null)
            => new() { IsSuccess = false, Message = message, Errors = errors ?? new() };
    }
}
