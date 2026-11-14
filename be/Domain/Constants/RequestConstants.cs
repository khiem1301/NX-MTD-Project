namespace NX_MTD.Domain.Constants;

public static class RequestTypes
{
    public const string SpareParts = "SPARE_PARTS";
    public const string NewMachine = "NEW_MACHINE";
    public const string MachineSupplies = "MACHINE_SUPPLIES";
    public const string AttendanceCorrection = "ATTENDANCE_CORRECTION";
    public const string MaterialSupplement = "MATERIAL_SUPPLEMENT";
    public const string DefectiveMaterial = "DEFECTIVE_MATERIAL";
    public const string ReplacementCutting = "REPLACEMENT_CUTTING";
    public const string ShortDelivery = "SHORT_DELIVERY";
    public const string MaterialRequisition = "MATERIAL_REQUISITION";
    public const string SemiFinishedRequisition = "SEMI_FINISHED_REQUISITION";
    /// <summary>Hệ thống tạo khi duyệt đơn NVL lỗi mà kho thiếu NVL: Quản đốc đọc rồi tự tạo đơn mua.</summary>
    public const string MaterialPurchase = "MATERIAL_PURCHASE";
    public const string Other = "OTHER";

    public static readonly IReadOnlyList<string> All = new[]
    {
        SpareParts, NewMachine, MachineSupplies, AttendanceCorrection, MaterialSupplement,
        DefectiveMaterial, ReplacementCutting, ShortDelivery, MaterialRequisition, SemiFinishedRequisition,
        MaterialPurchase, Other,
    };
}

public static class RequestStatuses
{
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string PendingProcessing = "PENDING_PROCESSING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Completed = "COMPLETED";

    /// <summary>Đã duyệt (hoặc chờ xử lý với loại không duyệt) nhưng người xử lý không làm được; kết thúc, kèm lý do ở ResultNote (DEV-ALL-98).</summary>
    public const string NotCompleted = "NOT_COMPLETED";
    public const string Cancelled = "CANCELLED";

    /// <summary>Đơn đã kết thúc mà không làm gì: không còn ràng buộc phiếu nguồn (VD phiếu hàng hỏng của đơn cắt bù).</summary>
    public static readonly IReadOnlySet<string> Dead = new HashSet<string> { Rejected, NotCompleted, Cancelled };

    /// <summary>Người tạo còn sửa / hủy được.</summary>
    public static readonly IReadOnlySet<string> Pending = new HashSet<string> { PendingApproval, PendingProcessing };
    public static readonly IReadOnlySet<string> Rejectable = new HashSet<string> { PendingApproval, PendingProcessing };
    public static readonly IReadOnlySet<string> Completable = new HashSet<string> { Approved, PendingProcessing };

    /// <summary>Người xử lý báo "Không hoàn thành" được khi đơn đang ở các trạng thái này (cùng điều kiện với Hoàn thành).</summary>
    public static readonly IReadOnlySet<string> NotCompletable = Completable;

    /// <summary>Mặc định của "Đơn cần xử lý".</summary>
    public static readonly IReadOnlySet<string> Open = new HashSet<string> { PendingApproval, PendingProcessing, Approved };
}
