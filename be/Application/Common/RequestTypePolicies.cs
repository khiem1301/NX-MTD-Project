using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Common;

/// <param name="CreatorTeamTypes">null = không giới hạn loại tổ; có giá trị = người tạo phải thuộc tổ loại này.</param>
/// <param name="HandlerChosenOnCreate">true = người tạo chọn vai trò xử lý (đơn Khác, DEV-ALL-91); HandlerRole để rỗng.</param>
public sealed record RequestTypePolicy(string RequestType, IReadOnlyCollection<string> CreatorRoles,
    IReadOnlyCollection<string>? CreatorTeamTypes, string HandlerRole, bool NeedsApproval, string Label,
    bool HandlerChosenOnCreate = false)
{
    public bool CanCreate(string role, string? teamTypeCode) =>
        CreatorRoles.Contains(role)
        && (CreatorTeamTypes is null || (teamTypeCode is not null && CreatorTeamTypes.Contains(teamTypeCode)));
}

/// <summary>AI Context 4.8. Đổi ai tạo / ai xử lý = sửa bảng này + ghi DEV (Coding Rules 8).</summary>
public static class RequestTypePolicies
{
    private static readonly string[] Leaders = { Roles.TeamLeader, Roles.TeamDeputy };
    private static readonly string[] NoCreator = Array.Empty<string>();
    private static readonly string[] CuttingOnly = { TeamTypes.Cutting };
    private static readonly string[] SewingOnly = { TeamTypes.Sewing };
    private static readonly string[] CuttingOrSewing = { TeamTypes.Cutting, TeamTypes.Sewing };
    private static readonly string[] Everyone =
    {
        Roles.Admin, Roles.Manager, Roles.Accountant, Roles.WarehouseKeeper, Roles.TeamLeader, Roles.TeamDeputy,
        Roles.QaQc, Roles.Electrician, Roles.Worker,
    };

    /// <summary>Vai trò chọn được cho đơn Khác (DEV-ALL-91).</summary>
    public static readonly IReadOnlyList<string> ChoosableHandlerRoles =
        new[] { Roles.Admin, Roles.Manager, Roles.Accountant, Roles.WarehouseKeeper, Roles.TeamLeader };

    public static readonly IReadOnlyDictionary<string, RequestTypePolicy> All = new[]
    {
        new RequestTypePolicy(RequestTypes.SpareParts, new[] { Roles.Electrician }, null, Roles.Admin, true, "Linh kiện"),
        new RequestTypePolicy(RequestTypes.NewMachine, new[] { Roles.Electrician }, null, Roles.Admin, true, "Máy mới"),
        new RequestTypePolicy(RequestTypes.MachineSupplies, new[] { Roles.Electrician }, null, Roles.Accountant, true, "Vật tư máy"),
        new RequestTypePolicy(RequestTypes.AttendanceCorrection, Leaders, CuttingOrSewing, Roles.Accountant, true, "Chấm công bù"),
        new RequestTypePolicy(RequestTypes.MaterialSupplement, new[] { Roles.TeamLeader, Roles.TeamDeputy, Roles.WarehouseKeeper },
            null, Roles.Manager, true, "Bổ sung NVL"),
        new RequestTypePolicy(RequestTypes.DefectiveMaterial, Leaders, CuttingOnly, Roles.Manager, true, "NVL lỗi"),
        new RequestTypePolicy(RequestTypes.ReplacementCutting, Leaders, SewingOnly, Roles.Manager, true, "Cắt bù"),
        new RequestTypePolicy(RequestTypes.ShortDelivery, NoCreator, null, Roles.Manager, false, "Thiếu hàng khi nhập"),
        new RequestTypePolicy(RequestTypes.MaterialRequisition, Leaders, CuttingOrSewing, Roles.WarehouseKeeper, true, "Xin NVL"),
        new RequestTypePolicy(RequestTypes.SemiFinishedRequisition, Leaders, SewingOnly, Roles.WarehouseKeeper, true, "Xin BTP"),
        new RequestTypePolicy(RequestTypes.MaterialPurchase, NoCreator, null, Roles.Manager, false, "Mua NVL"),
        new RequestTypePolicy(RequestTypes.Other, Everyone, null, string.Empty, true, "Khác", HandlerChosenOnCreate: true),
    }.ToDictionary(p => p.RequestType);

    public static readonly IReadOnlyDictionary<string, string> StatusLabels = new Dictionary<string, string>
    {
        [RequestStatuses.PendingApproval] = "Chờ duyệt",
        [RequestStatuses.PendingProcessing] = "Chờ xử lý",
        [RequestStatuses.Approved] = "Đã duyệt",
        [RequestStatuses.Rejected] = "Từ chối",
        [RequestStatuses.Completed] = "Hoàn thành",
        [RequestStatuses.NotCompleted] = "Không hoàn thành",
        [RequestStatuses.Cancelled] = "Đã hủy",
    };
}
