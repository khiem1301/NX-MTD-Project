using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Loại đơn chỉ có tiêu đề, nội dung, ảnh (không có dòng): ngày cần không ở quá khứ. Người xử lý được hoàn thành tay.</summary>
public abstract class NoLineRequestHandler : IRequestTypeHandler
{
    private readonly IClock _clock;
    private readonly string _noLinesMessage;

    protected NoLineRequestHandler(IClock clock, string noLinesMessage)
    {
        _clock = clock;
        _noLinesMessage = noLinesMessage;
    }

    public abstract string RequestType { get; }

    public bool AllowManualComplete => true;

    public Task<Result> ValidateAsync(RequestDraft draft, CancellationToken ct)
    {
        if (draft.Lines.Count > 0) return Task.FromResult(Invalid(_noLinesMessage));
        if (draft.RequestId is null && draft.TargetDate is { } target && target < _clock.Today)
            return Task.FromResult(Invalid(Messages.RequisitionTargetDatePast));
        return Task.FromResult(Result.Success());
    }

    public Task<Result> OnStatusChangedAsync(Request request, string oldStatus, CancellationToken ct) =>
        Task.FromResult(Result.Success());

    private static Result Invalid(string message) =>
        Result.Failure(ErrorCodes.RequestLinesInvalid, message, ResultKind.Validation);
}
