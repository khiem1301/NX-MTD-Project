using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

/// <param name="RequestType">Mã DB (UPPER_SNAKE).</param>
/// <param name="RequestId">null khi tạo mới.</param>
public sealed record RequestDraft(string RequestType, int? OrderId, int? WorkOrderId, int? KpiTargetId, int? MaterialIssueId,
    int? MaterialReceiptId, int? DefectRecordId, DateOnly? TargetDate, string? Title, string? Content,
    IReadOnlyList<RequestLineInput> Lines, int? RequestId);

/// <summary>Mỗi luồng thêm loại đơn của mình bằng một class + một dòng đăng ký DI. Không sửa RequestService.</summary>
public interface IRequestTypeHandler
{
    string RequestType { get; }

    /// <summary>false = đơn tự hoàn thành khi xong bước sau (vd. cắt bù hoàn thành khi BTP bù vào kho), người xử lý không bấm tay.</summary>
    bool AllowManualComplete { get; }

    /// <summary>Kiểm tra riêng khi tạo / sửa (bắt buộc dòng NVL, OrderId, ngày…). Trả Failure để chặn.</summary>
    Task<Result> ValidateAsync(RequestDraft draft, CancellationToken ct);

    /// <summary>Chạy TRONG transaction, sau khi đổi trạng thái, trước Commit. Không gọi SaveChanges / Commit.
    /// Trả Failure → rollback toàn bộ.</summary>
    Task<Result> OnStatusChangedAsync(Request request, string oldStatus, CancellationToken ct);
}

public sealed record RequestHeader(int RequestId, string RequestType, string Status, int? CreatedBy, int? CreatedByTeamId);

/// <param name="RequestTypes">Mã DB; null = không lọc.</param>
public sealed record RequestListFilter(PageQuery Page, int? CreatedBy, int? CreatedByTeamId,
    IReadOnlyCollection<string>? RequestTypes, IReadOnlyCollection<string>? Statuses);

public interface IRequestRepository : IGenericRepository<Request>
{
    /// <summary>Tracked, kèm RequestLines.</summary>
    Task<Request?> GetWithLinesAsync(int requestId, CancellationToken ct);
    Task<RequestHeader?> GetHeaderAsync(int requestId, CancellationToken ct);
    Task<string?> GetMaxCodeAsync(string prefix, CancellationToken ct);

    /// <summary>Tổ trưởng và tổ phó đang làm của một tổ (nhận thông báo đơn của riêng tổ).</summary>
    Task<IReadOnlyList<int>> TeamLeaderIdsAsync(int teamId, CancellationToken ct);
    Task<PagedResult<RequestListItemDto>> ListAsync(RequestListFilter filter, CancellationToken ct);

    /// <summary>Chưa có History / Permissions / MaxPhotos (service điền).</summary>
    Task<RequestDetailDto?> GetDetailAsync(int requestId, CancellationToken ct);
}

public interface IRequestService
{
    Task<Result<int>> CreateAsync(CreateRequestDto dto, CancellationToken ct);
    Task<Result> UpdateAsync(int requestId, UpdateRequestDto dto, CancellationToken ct);
    Task<Result> CancelAsync(int requestId, CancelRequestDto dto, CancellationToken ct);
    Task<Result> ApproveAsync(int requestId, ApproveRequestDto dto, CancellationToken ct);
    Task<Result> RejectAsync(int requestId, RejectRequestDto dto, CancellationToken ct);
    Task<Result> CompleteAsync(int requestId, CompleteRequestDto dto, CancellationToken ct);

    /// <summary>Hệ thống tạo đơn (SHORT_DELIVERY từ UC39 – Thái). Chỉ thêm vào DbContext, KHÔNG SaveChanges:
    /// người gọi lưu trong transaction của mình. Trả entity để gán thêm (vd. request.MaterialReceipt = receipt).</summary>
    /// <param name="teamId">Đơn của riêng một tổ (tăng ca): gán tổ và chỉ báo tổ trưởng / tổ phó của tổ đó.</param>
    /// <param name="autoApprovalNote">Có giá trị = hệ thống tự duyệt ngay với ghi chú này.</param>
    Task<Request> StageSystemRequestAsync(RequestDraft draft, CancellationToken ct, int? teamId = null, string? autoApprovalNote = null);

    /// <summary>Luồng khác hoàn thành đơn trong transaction của mình (vd. TH-05 khi tổ xác nhận phiếu cấp,
    /// cắt bù khi BTP bù vào kho). KHÔNG SaveChanges.</summary>
    Task<Result> MarkCompletedAsync(int requestId, string? resultNote, CancellationToken ct);
}

public interface IRequestQueryService
{
    IReadOnlyList<RequestTypeOptionDto> GetCreatableTypes();
    Task<Result<PagedResult<RequestListItemDto>>> ListMineAsync(RequestQuery query, CancellationToken ct);
    Task<Result<PagedResult<RequestListItemDto>>> ListInboxAsync(RequestQuery query, CancellationToken ct);
    Task<Result<RequestDetailDto>> GetDetailAsync(int requestId, CancellationToken ct);
}
