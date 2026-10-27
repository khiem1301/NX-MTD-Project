using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Common;

/// <summary>
/// Số bộ BTP tổ may đã nhận từ kho (26/10, thay vw_TeamPieceHolding): dòng bộ đủ của phiếu cấp BTP đã nhận,
/// cùng công thức trigger trg_WorkAllocation_Check dùng làm trần giao việc (50050).
/// </summary>
public static class TeamPieceQueries
{
    public static IQueryable<MaterialIssueLine> ReceivedSets(this IQueryable<MaterialIssueLine> lines, int? sewingTeamId,
        int? orderLineId = null) =>
        lines.Where(l => l.MaterialIssue.WarehouseType == WarehouseTypes.SemiFinished
                         && l.MaterialIssue.Status == IssueStatuses.Received
                         && l.ProductComponentMaterialId == null && l.OrderLineId != null
                         && (sewingTeamId == null || l.MaterialIssue.ReceivingTeamId == sewingTeamId)
                         && (orderLineId == null || l.OrderLineId == orderLineId));
}
