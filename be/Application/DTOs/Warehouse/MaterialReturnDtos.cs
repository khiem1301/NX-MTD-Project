using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record MaterialReturnListItemDto(
    int MaterialReturnId, string ReturnCode, int MaterialIssueId, string IssueCode, string OrderCode, int CuttingTeamId,
    string CuttingTeamName, string Status, DateTime CreatedAt, string? ConfirmedByName, DateTime? ConfirmedAt,
    decimal TotalReturnedQty);

public sealed record MaterialReturnLineDto(
    int MaterialReturnLineId, int MaterialIssueLineId, string MaterialCode, string MaterialName, string Unit,
    string MaterialColor, decimal ReturnedQty, decimal? ConfirmedQty, string? DiscrepancyReason);

public sealed record MaterialReturnDto(
    int MaterialReturnId, string ReturnCode, int MaterialIssueId, string IssueCode, string OrderCode, int CuttingTeamId,
    string CuttingTeamName, string? Note, string Status, DateTime CreatedAt, string? ConfirmedByName, DateTime? ConfirmedAt,
    string? RejectReason, IReadOnlyList<MaterialReturnLineDto> Lines);

/// <summary>Vải đã cấp cho tổ cắt (phiếu cấp đã nhận) và số còn trả được của từng dòng.</summary>
/// <param name="SuggestedQty">
/// Số vải còn lại gợi ý trả: đã nhận − đã trả / đang trả − đã cắt (Σ số mét vải đã dùng các bàn của tổ trên đơn), chia vào các
/// dòng phiếu cấp cùng đơn, cùng vải. null khi có bàn cắt chưa nhập chiều dài (chưa tính được số đã cắt).
/// </param>
public sealed record ReturnableLineDto(
    int MaterialIssueId, string IssueCode, int MaterialIssueLineId, string OrderCode, string MaterialCode,
    string MaterialName, string Unit, string MaterialColor, decimal ReceivedQty, decimal ReturnedQty, decimal RemainingQty,
    decimal? SuggestedQty = null);

public sealed record CreateReturnLineDto(int MaterialIssueLineId, decimal ReturnedQty);

public sealed record CreateMaterialReturnDto(int MaterialIssueId, string? Note, IReadOnlyList<CreateReturnLineDto> Lines);

public sealed record ConfirmReturnLineDto(int MaterialReturnLineId, decimal ConfirmedQty, string? DiscrepancyReason);

public sealed record ConfirmMaterialReturnDto(IReadOnlyList<ConfirmReturnLineDto> Lines);

public sealed record MaterialReturnQuery : PageQuery
{
    public string? Status { get; init; }
}
