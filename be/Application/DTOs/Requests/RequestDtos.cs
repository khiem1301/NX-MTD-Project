using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

/// <summary>Dòng đơn chung: NVL (MaterialId, đã gồm màu), BTP theo dòng đơn (OrderLineId), chi tiết cắt bù (ProductComponentMaterialId),
/// hoặc mô tả linh kiện / máy (ItemDescription). Loại đơn nào dùng cột nào do IRequestTypeHandler kiểm.</summary>
public sealed record RequestLineInput(int? MaterialId, int? OrderLineId, int? ProductComponentMaterialId,
    string? ItemDescription, decimal Quantity);

/// <param name="RequestType">PascalCase, vd. "SpareParts".</param>
public sealed record CreateRequestDto(string RequestType, string? Title, string? Content, DateOnly? TargetDate,
    int? OrderId, int? WorkOrderId, int? KpiTargetId, int? MaterialIssueId, int? DefectRecordId,
    IReadOnlyList<RequestLineInput>? Lines);

public sealed record UpdateRequestDto(string? Title, string? Content, DateOnly? TargetDate,
    IReadOnlyList<RequestLineInput>? Lines, byte[] RowVer);

public sealed record ApproveRequestDto(string? Note, byte[] RowVer);
public sealed record RejectRequestDto(string Reason, byte[] RowVer);
public sealed record CompleteRequestDto(string? ResultNote, byte[] RowVer);
public sealed record CancelRequestDto(string? Reason, byte[] RowVer);

public sealed record RequestQuery : PageQuery
{
    public string? RequestType { get; init; }   // PascalCase
    public string? Status { get; init; }        // PascalCase
}

public sealed record RequestTypeOptionDto(string RequestType, string Label, bool NeedsApproval, string HandlerRole);

public sealed record RequestListItemDto(int RequestId, string RequestCode, string RequestType, string Status,
    string? Title, DateOnly? TargetDate, string? CreatedByName, string? TeamName, DateTime CreatedAt, DateTime? DecidedAt);

public sealed record RequestLineDto(int RequestLineId, int? MaterialId, string? MaterialName, string? MaterialColor,
    int? OrderLineId, string? OrderLineLabel, int? ProductComponentMaterialId, string? ComponentName, string? ItemDescription, decimal Quantity, decimal? ReplenishQty);

public sealed record RequestPermissionsDto(bool CanEdit, bool CanCancel, bool CanApprove, bool CanReject, bool CanComplete)
{
    public static readonly RequestPermissionsDto None = new(false, false, false, false, false);
}

public sealed record RequestDetailDto
{
    public int RequestId { get; init; }
    public string RequestCode { get; init; } = string.Empty;
    public string RequestType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string? Content { get; init; }
    public DateOnly? TargetDate { get; init; }
    public int? OrderId { get; init; }
    public string? OrderCode { get; init; }
    public int? CreatedBy { get; init; }
    public string? CreatedByName { get; init; }
    public int? CreatedByTeamId { get; init; }
    public string? TeamName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? DecidedAt { get; init; }
    public string? DecisionNote { get; init; }
    public string? ResultNote { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ShortageResolution { get; init; }
    public byte[] RowVer { get; init; } = Array.Empty<byte>();
    public IReadOnlyList<RequestLineDto> Lines { get; init; } = Array.Empty<RequestLineDto>();
    public IReadOnlyList<AuditLogDto> History { get; init; } = Array.Empty<AuditLogDto>();
    public RequestPermissionsDto Permissions { get; init; } = RequestPermissionsDto.None;
    public int MaxPhotos { get; init; }
}
