using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Requests;

/// <summary>Loại đơn chưa có handler riêng dùng DefaultHandler (không kiểm thêm, được hoàn thành tay).</summary>
public sealed class RequestTypeHandlers
{
    private readonly Dictionary<string, IRequestTypeHandler> _handlers;
    public RequestTypeHandlers(IEnumerable<IRequestTypeHandler> handlers) => _handlers = handlers.ToDictionary(h => h.RequestType);

    public IRequestTypeHandler For(string requestType) =>
        _handlers.TryGetValue(requestType, out var h) ? h : DefaultHandler.Instance;

    private sealed class DefaultHandler : IRequestTypeHandler
    {
        public static readonly DefaultHandler Instance = new();
        public string RequestType => "*";
        public bool AllowManualComplete => true;
        public Task<Result> ValidateAsync(RequestDraft draft, CancellationToken ct) => Task.FromResult(Result.Success());
        public Task<Result> OnStatusChangedAsync(Request request, string oldStatus, CancellationToken ct) => Task.FromResult(Result.Success());
    }
}
