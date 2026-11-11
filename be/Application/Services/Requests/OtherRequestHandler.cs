using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Đơn Khác (DEV-ALL-91): chỉ có nội dung, không có dòng; ngày cần không ở quá khứ.</summary>
public sealed class OtherRequestHandler : IRequestTypeHandler
{
    private readonly IClock _clock;

    public OtherRequestHandler(IClock clock) => _clock = clock;

    public string RequestType => RequestTypes.Other;

    public bool AllowManualComplete => true;

    public Task<Result> ValidateAsync(RequestDraft draft, CancellationToken ct)
    {
        if (draft.Lines.Count > 0) return Task.FromResult(Invalid(Messages.OtherRequestNoLines));
        if (draft.RequestId is null && draft.TargetDate is { } target && target < _clock.Today)
            return Task.FromResult(Invalid(Messages.RequisitionTargetDatePast));
        return Task.FromResult(Result.Success());
    }

    public Task<Result> OnStatusChangedAsync(Request request, string oldStatus, CancellationToken ct) =>
        Task.FromResult(Result.Success());

    private static Result Invalid(string message) =>
        Result.Failure(ErrorCodes.RequestLinesInvalid, message, ResultKind.Validation);
}
