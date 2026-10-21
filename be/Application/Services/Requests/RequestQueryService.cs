using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Requests;

public sealed class RequestQueryService : IRequestQueryService
{
    private const int DefaultMaxPhotos = 5;

    private readonly IRequestRepository _requests;
    private readonly ICurrentUser _user;
    private readonly IAuditLogService _audit;
    private readonly ISystemSettingService _settings;
    private readonly RequestAccess _access;
    private readonly RequestTypeHandlers _handlers;

    public RequestQueryService(IRequestRepository requests, ICurrentUser user, IAuditLogService audit,
        ISystemSettingService settings, RequestAccess access, RequestTypeHandlers handlers)
    {
        _requests = requests; _user = user; _audit = audit; _settings = settings; _access = access; _handlers = handlers;
    }

    public IReadOnlyList<RequestTypeOptionDto> GetCreatableTypes() =>
        _access.CreatableTypes()
            .Select(p => new RequestTypeOptionDto(CodeText.ToPascal(p.RequestType), p.Label, p.NeedsApproval, p.HandlerRole))
            .ToList();

    public async Task<Result<PagedResult<RequestListItemDto>>> ListMineAsync(RequestQuery query, CancellationToken ct)
    {
        var filter = BuildFilter(query, null, out var invalid);
        if (invalid is not null) return Fail.As<PagedResult<RequestListItemDto>>(invalid);

        if (Roles.IsTeamRole(_user.Role))
        {
            if (_user.TeamId is not int teamId)
                return Result<PagedResult<RequestListItemDto>>.Success(
                    new PagedResult<RequestListItemDto>(Array.Empty<RequestListItemDto>(), query.SafePage, query.SafePageSize, 0));
            filter = filter with { CreatedByTeamId = teamId };
        }
        else
        {
            filter = filter with { CreatedBy = _user.EmployeeId };
        }
        return Result<PagedResult<RequestListItemDto>>.Success(await _requests.ListAsync(filter, ct));
    }

    public async Task<Result<PagedResult<RequestListItemDto>>> ListInboxAsync(RequestQuery query, CancellationToken ct)
    {
        var handled = _access.HandledTypes();
        var filter = BuildFilter(query, RequestStatuses.Open, out var invalid);
        if (invalid is not null) return Fail.As<PagedResult<RequestListItemDto>>(invalid);

        var types = filter.RequestTypes is null ? handled : handled.Intersect(filter.RequestTypes).ToList();
        return Result<PagedResult<RequestListItemDto>>.Success(
            await _requests.ListAsync(filter with { RequestTypes = types }, ct));
    }

    public async Task<Result<RequestDetailDto>> GetDetailAsync(int requestId, CancellationToken ct)
    {
        var detail = await _requests.GetDetailAsync(requestId, ct);
        if (detail is null) return Fail.NotFound<RequestDetailDto>();

        var type = CodeText.ToCode(detail.RequestType);
        if (!_access.CanView(type, detail.CreatedBy, detail.CreatedByTeamId)) return Fail.NotFound<RequestDetailDto>();

        var history = await _audit.GetHistoryAsync(nameof(Request), requestId, ct);
        var permissions = _access.PermissionsFor(type, CodeText.ToCode(detail.Status), detail.CreatedBy,
            detail.CreatedByTeamId, _handlers.For(type).AllowManualComplete);
        var maxPhotos = await _settings.GetIntAsync(SettingKeys.RequestMaxPhotos, DefaultMaxPhotos, ct);
        return Result<RequestDetailDto>.Success(detail with { History = history, Permissions = permissions, MaxPhotos = maxPhotos });
    }

    /// <summary>Đổi RequestType / Status PascalCase sang mã DB; giá trị lạ → 400. defaultStatuses dùng khi không truyền Status.</summary>
    private static RequestListFilter BuildFilter(RequestQuery q, IReadOnlySet<string>? defaultStatuses, out Result? invalid)
    {
        invalid = null;
        IReadOnlyCollection<string>? types = null;
        IReadOnlyCollection<string>? statuses = defaultStatuses;

        if (!string.IsNullOrWhiteSpace(q.RequestType))
        {
            var code = CodeText.ToCode(q.RequestType);
            if (!RequestTypes.All.Contains(code)) invalid = BadFilter("requestType");
            types = new[] { code };
        }
        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            var code = CodeText.ToCode(q.Status);
            if (!RequestTypePolicies.StatusLabels.ContainsKey(code)) invalid = BadFilter("status");
            statuses = new[] { code };
        }
        return new RequestListFilter(q, null, null, types, statuses);
    }

    private static Result BadFilter(string field) => Result.Failure(ErrorCodes.CommonValidation, "Dữ liệu không hợp lệ.",
        ResultKind.Validation, new Dictionary<string, string[]> { [field] = new[] { "Giá trị không hợp lệ." } });
}
