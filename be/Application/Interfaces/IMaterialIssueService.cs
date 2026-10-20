using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC40 / UC41: phiếu cấp NVL cho tổ cắt, BTP cho tổ may; tổ trưởng xác nhận (trừ kho) hoặc từ chối.</summary>
public interface IMaterialIssueService
{
    /// <summary>receivingTeamId khác null: chỉ phiếu cấp cho tổ đó (tổ trưởng xem phiếu của tổ mình).</summary>
    Task<Result<PagedResult<MaterialIssueListItemDto>>> GetPagedAsync(MaterialIssueQuery query, int? receivingTeamId,
        CancellationToken ct);

    Task<Result<MaterialIssueDto>> GetByIdAsync(int issueId, int? receivingTeamId, CancellationToken ct);

    /// <summary>Thủ kho lập phiếu: kiểm tra đủ khả dụng rồi giữ chỗ (trạng thái PENDING_RECEIPT).</summary>
    Task<Result<MaterialIssueDto>> CreateAsync(CreateMaterialIssueDto dto, int issuedByEmployeeId, CancellationToken ct);

    /// <summary>Tổ trưởng xác nhận số thực nhận: trừ tồn theo số xác nhận, phần chênh lệch ở lại kho.</summary>
    Task<Result> ConfirmAsync(int issueId, ConfirmIssueDto dto, int teamId, int employeeId, CancellationToken ct);

    /// <summary>Tổ trưởng từ chối: nhả giữ chỗ, không trừ kho.</summary>
    Task<Result> RejectAsync(int issueId, ReasonDto dto, int teamId, CancellationToken ct);
}
