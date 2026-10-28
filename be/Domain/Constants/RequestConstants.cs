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
    /// <summary>Tăng ca khi KPI không đạt: hệ thống tạo lúc kết thúc đơn của tổ; tổ trưởng quyết định (thiếu ít thì tự duyệt).</summary>
    public const string Overtime = "OVERTIME";

    public static readonly IReadOnlyList<string> All = new[]
    {
        SpareParts, NewMachine, MachineSupplies, AttendanceCorrection, MaterialSupplement,
        DefectiveMaterial, ReplacementCutting, ShortDelivery, MaterialRequisition, SemiFinishedRequisition, Overtime,
    };
}

public static class RequestStatuses
{
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string PendingProcessing = "PENDING_PROCESSING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";

    /// <summary>Người tạo còn sửa / hủy được.</summary>
    public static readonly IReadOnlySet<string> Pending = new HashSet<string> { PendingApproval, PendingProcessing };
    public static readonly IReadOnlySet<string> Rejectable = new HashSet<string> { PendingApproval, PendingProcessing };
    public static readonly IReadOnlySet<string> Completable = new HashSet<string> { Approved, PendingProcessing };

    /// <summary>Mặc định của "Đơn cần xử lý".</summary>
    public static readonly IReadOnlySet<string> Open = new HashSet<string> { PendingApproval, PendingProcessing, Approved };
}
