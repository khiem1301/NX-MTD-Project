using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Requests;

public sealed class RequestService : IRequestService
{
    private const string MineLink = "/requests?id={0}";
    private const string InboxLink = "/requests/inbox";

    private readonly IRequestRepository _requests;
    private readonly IGenericRepository<RequestLine> _lines;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditLogService _audit;
    private readonly INotificationService _notifier;
    private readonly RequestAccess _access;
    private readonly RequestTypeHandlers _handlers;

    public RequestService(IRequestRepository requests, IGenericRepository<RequestLine> lines, IUnitOfWork uow,
        ICurrentUser user, IClock clock, IAuditLogService audit, INotificationService notifier,
        RequestAccess access, RequestTypeHandlers handlers)
    {
        _requests = requests; _lines = lines; _uow = uow; _user = user; _clock = clock;
        _audit = audit; _notifier = notifier; _access = access; _handlers = handlers;
    }

    public async Task<Result<int>> CreateAsync(CreateRequestDto dto, CancellationToken ct)
    {
        var type = CodeText.ToCode(dto.RequestType);
        if (!RequestTypePolicies.All.TryGetValue(type, out var policy) || !policy.CanCreate(_user.Role, _user.TeamTypeCode))
            return Result<int>.Failure(ErrorCodes.RequestTypeNotAllowed, Messages.RequestTypeNotAllowed, ResultKind.Forbidden);

        var draft = new RequestDraft(type, dto.OrderId, dto.WorkOrderId, dto.KpiTargetId, dto.MaterialIssueId, null,
            dto.DefectRecordId, dto.TargetDate, dto.Title, dto.Content, dto.Lines ?? Array.Empty<RequestLineInput>(), null);
        var check = await _handlers.For(type).ValidateAsync(draft, ct);
        if (!check.IsSuccess) return Fail.As<int>(check);

        var request = await NewRequestAsync(draft, policy, _user.EmployeeId, _user.TeamId, ct);
        await _requests.AddAsync(request);
        await NotifyHandlersAsync(policy, request, ct);
        await _uow.SaveChangesAsync();   // D18: không ghi AuditLog INSERT
        return Result<int>.Success(request.RequestId, Messages.Submitted);   // MSG10
    }

    public async Task<Request> StageSystemRequestAsync(RequestDraft draft, CancellationToken ct)
    {
        var policy = RequestTypePolicies.All[draft.RequestType];
        var request = await NewRequestAsync(draft, policy, null, null, ct);
        await _requests.AddAsync(request);
        await NotifyHandlersAsync(policy, request, ct);
        return request;
    }

    public async Task<Result> UpdateAsync(int requestId, UpdateRequestDto dto, CancellationToken ct)
    {
        var request = await _requests.GetWithLinesAsync(requestId, ct);
        if (request is null || !_access.IsCreatorSide(request.CreatedBy, request.CreatedByTeamId)) return Fail.NotFound();
        if (!RequestStatuses.Pending.Contains(request.Status))
            return Result.Failure(ErrorCodes.RequestInvalidStatus, Messages.RequestNotEditable);

        var lines = dto.Lines ?? Array.Empty<RequestLineInput>();
        var draft = new RequestDraft(request.RequestTypeCode, request.OrderId, request.WorkOrderId, request.KpiTargetId,
            request.MaterialIssueId, request.MaterialReceiptId, request.DefectRecordId, dto.TargetDate, dto.Title, dto.Content,
            lines, request.RequestId);
        var check = await _handlers.For(request.RequestTypeCode).ValidateAsync(draft, ct);
        if (!check.IsSuccess) return check;

        _requests.SetOriginalRowVersion(request, dto.RowVer);
        var before = new { request.Title, request.Content, request.TargetDate, LineCount = request.RequestLines.Count };
        request.Title = dto.Title?.Trim();
        request.Content = dto.Content?.Trim();
        request.TargetDate = dto.TargetDate;
        request.UpdatedAt = _clock.Now;
        ReplaceLines(request, lines);
        _audit.Add(new AuditEntry(nameof(Request), request.RequestId, AuditActions.Update, before,
            new { request.Title, request.Content, request.TargetDate, LineCount = lines.Count }));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    public async Task<Result> CancelAsync(int requestId, CancelRequestDto dto, CancellationToken ct)
    {
        var request = await _requests.GetByIdAsync(requestId);
        if (request is null || !_access.IsCreatorSide(request.CreatedBy, request.CreatedByTeamId)) return Fail.NotFound();
        if (!RequestStatuses.Pending.Contains(request.Status) || request.CreatedBy is null)
            return Result.Failure(ErrorCodes.RequestInvalidStatus, Messages.RequestNotEditable);

        _requests.SetOriginalRowVersion(request, dto.RowVer);
        var from = request.Status;
        request.Status = RequestStatuses.Cancelled;
        request.UpdatedAt = _clock.Now;
        return await CommitTransitionAsync(request, from, AuditActions.Update, dto.Reason?.Trim(), notifyCreator: false, ct);
    }

    public Task<Result> ApproveAsync(int requestId, ApproveRequestDto dto, CancellationToken ct) =>
        DecideAsync(requestId, dto.RowVer, RequestStatuses.Approved, AuditActions.Approve, dto.Note, ct);

    public Task<Result> RejectAsync(int requestId, RejectRequestDto dto, CancellationToken ct) =>
        DecideAsync(requestId, dto.RowVer, RequestStatuses.Rejected, AuditActions.Reject, dto.Reason, ct);

    public async Task<Result> CompleteAsync(int requestId, CompleteRequestDto dto, CancellationToken ct)
    {
        var request = await _requests.GetByIdAsync(requestId);
        if (request is null || !_access.IsHandler(request.RequestTypeCode)) return Fail.NotFound();
        if (!_handlers.For(request.RequestTypeCode).AllowManualComplete)
            return Result.Failure(ErrorCodes.RequestManualCompleteNotAllowed, Messages.RequestManualCompleteNotAllowed);
        if (!RequestStatuses.Completable.Contains(request.Status))
            return Result.Failure(ErrorCodes.RequestInvalidStatus, Messages.RequestAlreadyDecided);

        _requests.SetOriginalRowVersion(request, dto.RowVer);
        var from = request.Status;
        SetCompleted(request, dto.ResultNote);
        return await CommitTransitionAsync(request, from, AuditActions.Confirm, null, notifyCreator: true, ct);
    }

    public async Task<Result> MarkCompletedAsync(int requestId, string? resultNote, CancellationToken ct)
    {
        var request = await _requests.GetByIdAsync(requestId);
        if (request is null) return Fail.NotFound();
        if (!RequestStatuses.Completable.Contains(request.Status))
            return Result.Failure(ErrorCodes.RequestInvalidStatus, Messages.RequestAlreadyDecided);
        var from = request.Status;
        SetCompleted(request, resultNote);
        AddTransitionLog(request, from, AuditActions.Confirm, null);
        NotifyCreator(request);
        return Result.Success();
    }

    private async Task<Result> DecideAsync(int requestId, byte[] rowVer, string to, string action, string? note,
        CancellationToken ct)
    {
        var request = await _requests.GetByIdAsync(requestId);
        if (request is null || !_access.IsHandler(request.RequestTypeCode)) return Fail.NotFound();
        var allowed = to == RequestStatuses.Approved
            ? request.Status == RequestStatuses.PendingApproval
            : RequestStatuses.Rejectable.Contains(request.Status);
        if (!allowed) return Result.Failure(ErrorCodes.RequestInvalidStatus, Messages.RequestAlreadyDecided);   // GBR-05

        _requests.SetOriginalRowVersion(request, rowVer);
        var from = request.Status;
        request.Status = to;
        request.DecisionNote = note?.Trim();
        request.DecidedAt = _clock.Now;
        request.UpdatedAt = _clock.Now;
        return await CommitTransitionAsync(request, from, action, null, notifyCreator: true, ct);
    }

    /// <summary>Log + hook của loại đơn + thông báo trong một transaction (D17).</summary>
    private Task<Result> CommitTransitionAsync(Request request, string from, string action, string? reason,
        bool notifyCreator, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            AddTransitionLog(request, from, action, reason);
            var hook = await _handlers.For(request.RequestTypeCode).OnStatusChangedAsync(request, from, ct);
            if (!hook.IsSuccess) return hook;
            if (notifyCreator) NotifyCreator(request);
            await _uow.SaveChangesAsync();
            return Result.Success(SuccessMessage(request.Status));
        });

    private void AddTransitionLog(Request request, string from, string action, string? reason) =>
        _audit.Add(new AuditEntry(nameof(Request), request.RequestId, action, new { Status = from },
            new { request.Status, request.DecisionNote, request.ResultNote }, reason));

    private void SetCompleted(Request request, string? resultNote)
    {
        request.Status = RequestStatuses.Completed;
        request.ResultNote = resultNote?.Trim();
        request.CompletedAt = _clock.Now;
        request.UpdatedAt = _clock.Now;
    }

    private async Task<Request> NewRequestAsync(RequestDraft d, RequestTypePolicy policy, int? createdBy, int? teamId,
        CancellationToken ct)
    {
        var request = new Request
        {
            RequestCode = await NextCodeAsync(ct),
            RequestTypeCode = d.RequestType,
            Status = policy.NeedsApproval ? RequestStatuses.PendingApproval : RequestStatuses.PendingProcessing,
            OrderId = d.OrderId, WorkOrderId = d.WorkOrderId, KpiTargetId = d.KpiTargetId,
            MaterialIssueId = d.MaterialIssueId, MaterialReceiptId = d.MaterialReceiptId, DefectRecordId = d.DefectRecordId,
            TargetDate = d.TargetDate, Title = d.Title?.Trim(), Content = d.Content?.Trim(),
            CreatedBy = createdBy, CreatedByTeamId = teamId, CreatedAt = _clock.Now,
        };
        foreach (var line in d.Lines) request.RequestLines.Add(ToEntity(line));
        return request;
    }

    /// <summary>D25: YC&lt;yyMMdd&gt;-&lt;NNN&gt;. Hai người lưu cùng lúc → unique lỗi → middleware trả 409 COMMON_DUPLICATE.</summary>
    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var prefix = $"YC{_clock.Today:yyMMdd}-";
        var max = await _requests.GetMaxCodeAsync(prefix, ct);
        var next = max is { Length: > 0 } && max.Length > prefix.Length && int.TryParse(max[prefix.Length..], out var n) ? n + 1 : 1;
        return $"{prefix}{next:D3}";
    }

    private void ReplaceLines(Request request, IReadOnlyList<RequestLineInput> lines)
    {
        foreach (var old in request.RequestLines.ToList()) _lines.Remove(old);   // FK không cascade: xóa qua DbSet
        foreach (var line in lines) request.RequestLines.Add(ToEntity(line));
    }

    private static RequestLine ToEntity(RequestLineInput l) => new()
    {
        MaterialId = l.MaterialId, MaterialColor = l.MaterialColor?.Trim(), OrderLineId = l.OrderLineId,
        ProductComponentId = l.ProductComponentId, ItemDescription = l.ItemDescription?.Trim(), Quantity = l.Quantity,
    };

    private Task NotifyHandlersAsync(RequestTypePolicy policy, Request request, CancellationToken ct) =>
        _notifier.NotifyRoleAsync(Roles.ToRoleCode(policy.HandlerRole), new NotificationMessage(
            NotificationSources.Request, Messages.NotifyNewRequestTitle,
            string.Format(Messages.NotifyNewRequestBody, request.RequestCode, policy.Label), InboxLink), ct);

    private void NotifyCreator(Request request)
    {
        if (request.CreatedBy is not int creator) return;
        var status = RequestTypePolicies.StatusLabels[request.Status];
        _notifier.NotifyEmployees(new[] { creator }, new NotificationMessage(NotificationSources.Request,
            Messages.NotifyRequestChangedTitle, string.Format(Messages.NotifyRequestChangedBody, request.RequestCode, status),
            string.Format(MineLink, request.RequestId), nameof(Request), request.RequestId));
    }

    private static string SuccessMessage(string status) => status switch
    {
        RequestStatuses.Approved => Messages.Confirmed,           // MSG11
        RequestStatuses.Rejected => Messages.Returned,            // MSG12
        RequestStatuses.Completed => Messages.RequestCompleted,
        RequestStatuses.Cancelled => Messages.RequestCancelled,
        _ => Messages.Saved,
    };
}
