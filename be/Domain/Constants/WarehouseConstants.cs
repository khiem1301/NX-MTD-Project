namespace NX_MTD.Domain.Constants;

/// <summary>Giá trị khớp CHECK constraint trong DB. Không tự đặt chuỗi khác.</summary>
public static class WarehouseTypes
{
    public const string Material = "MATERIAL";
    public const string SemiFinished = "SEMI_FINISHED";
    public const string Finished = "FINISHED";

    public static readonly IReadOnlyList<string> All = new[] { Material, SemiFinished, Finished };
}

public static class StockDocTypes
{
    public const string MaterialReceipt = "MATERIAL_RECEIPT";
    public const string MaterialIssue = "MATERIAL_ISSUE";
    public const string MaterialReturn = "MATERIAL_RETURN";
    public const string CutHandover = "CUT_HANDOVER";
    public const string FgHandover = "FG_HANDOVER";
    public const string DispatchNote = "DISPATCH_NOTE";
    public const string Stocktake = "STOCKTAKE";
}

/// <summary>ProductComponent.ComponentType (CK_PC_Type): chi tiết cắt từ vải hay phụ liệu cấp thẳng cho tổ may.</summary>
public static class ComponentTypes
{
    public const string Panel = "PANEL";
    public const string Trim = "TRIM";
}

public static class MaterialTypes
{
    public const string Fabric = "FABRIC";
    public const string Trim = "TRIM";

    /// <summary>Nhóm hợp lệ theo loại (CHECK constraint của bảng Material).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Categories =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Fabric] = new[] { "MAIN_FABRIC", "LINING", "INTERLINING", "RIB" },
            [Trim] = new[] { "THREAD", "ZIPPER", "BUTTON", "SNAP", "ELASTIC", "TAPE", "LABEL", "HANG_TAG", "PADDING", "PACKAGING", "OTHER" },
        };

    public static bool IsValidCategory(string materialType, string category) =>
        Categories.TryGetValue(materialType, out var list) && list.Contains(category);
}

public static class MaterialStatuses
{
    public const string Active = "ACTIVE";
    public const string Discontinued = "DISCONTINUED";
}

public static class TeamTypes
{
    public const string Office = "OFFICE";
    public const string Cutting = "CUTTING";
    public const string Sewing = "SEWING";
}

public static class OrderStatuses
{
    public const string New = "NEW";
    public const string InProduction = "IN_PRODUCTION";
    public const string InDelivery = "IN_DELIVERY";
    public const string PartiallyDelivered = "PARTIALLY_DELIVERED";
    public const string Cancelled = "CANCELLED";
    public const string Stopped = "STOPPED";
    public const string Delivered = "DELIVERED";
    public const string Completed = "COMPLETED";

    /// <summary>Đơn đã đóng thì không lập thêm phiếu kho / lệnh cắt.</summary>
    public static readonly IReadOnlyList<string> Closed = new[] { Cancelled, Stopped, Delivered, Completed };

    /// <summary>D27: đang sản xuất / giao thì chỉ sửa số lượng, đơn giá, thêm dòng.</summary>
    public static readonly IReadOnlySet<string> LimitedEdit = new HashSet<string> { InProduction, InDelivery, PartiallyDelivered };

    /// <summary>Được dừng (bắt buộc lý do).</summary>
    public static readonly IReadOnlySet<string> Stoppable = LimitedEdit;

    /// <summary>Mặc định của tra cứu cho luồng khác: đơn còn đang chạy.</summary>
    public static readonly IReadOnlySet<string> Running = new HashSet<string> { New, InProduction, InDelivery, PartiallyDelivered };
}

public static class ReceiptStatuses
{
    public const string Pending = "PENDING";
    public const string Confirmed = "CONFIRMED";
    public const string Returned = "RETURNED";
}

public static class ReceiptTypes
{
    public const string New = "NEW";
    public const string Supplementary = "SUPPLEMENTARY";
}

/// <summary>Request.ShortageResolution (CK_Request_Shortage): cách Quản đốc xử lý hàng thiếu / lỗi khi nhập.</summary>
public static class ShortageResolutions
{
    public const string Replenish = "REPLENISH";
    public const string NoReplenish = "NO_REPLENISH";
}

public static class MaterialSources
{
    public const string Customer = "CUSTOMER";
    public const string Supplier = "SUPPLIER";
}

public static class IssueStatuses
{
    public const string PendingReceipt = "PENDING_RECEIPT";
    public const string Received = "RECEIVED";
    public const string Rejected = "REJECTED";
}

public static class WorkOrderStatuses
{
    public const string Open = "OPEN";
    public const string Cutting = "CUTTING";
    /// <summary>Tự động khi BTP đã vào kho (phiếu bàn giao được xác nhận) đủ số kế hoạch của mọi dòng; không có nút đóng tay.</summary>
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
}

public static class CuttingLayStatuses
{
    public const string Recorded = "RECORDED";
    public const string Locked = "LOCKED";
}

public static class HandoverStatuses
{
    public const string Pending = "PENDING";
    public const string Confirmed = "CONFIRMED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";
}

/// <summary>VehicleTrip.Status (CK_VT_Status).</summary>
public static class TripStatuses
{
    public const string Started = "STARTED";
    public const string Completed = "COMPLETED";
}

/// <summary>Stocktake.Status (CK_Stocktake_Status).</summary>
public static class StocktakeStatuses
{
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Returned = "RETURNED";
}

/// <summary>WorkAllocation.MovementType (CK_WA_Type): tổ trưởng giao việc (kèm BTP) cho công nhân hoặc thu hồi lại.</summary>
public static class PieceMovementTypes
{
    public const string Handover = "HANDOVER";
    public const string Retrieval = "RETRIEVAL";
}

/// <summary>WorkAllocation.RetrievalReason (CK_WA_Retr).</summary>
public static class RetrievalReasons
{
    public const string OperationChanged = "OPERATION_CHANGED";
    public const string WorkerAbsent = "WORKER_ABSENT";
    public const string StyleFinished = "STYLE_FINISHED";

    public static readonly IReadOnlyList<string> All = new[] { OperationChanged, WorkerAbsent, StyleFinished };
}

/// <summary>WorkAllocation.ConfirmMode (CK_WA_Mode): công nhân tự xác nhận hoặc tổ trưởng xác nhận hộ.</summary>
public static class ConfirmModes
{
    public const string Worker = "WORKER";
    public const string OnBehalf = "ON_BEHALF";
}

/// <summary>DispatchNote.Status (CK_DN_Status). Giao hàng luôn thành công, không có trạng thái giao thất bại (GBR-24).</summary>
public static class DispatchStatuses
{
    public const string Pending = "PENDING";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";
    public const string InDelivery = "IN_DELIVERY";
    public const string Delivered = "DELIVERED";
    public const string PartiallyDelivered = "PARTIALLY_DELIVERED";

    /// <summary>Phiếu đã hoặc đang chiếm số hàng của đơn (tính vào tổng đã xuất).</summary>
    public static readonly IReadOnlySet<string> Issuing = new HashSet<string> { Pending, InDelivery, Delivered, PartiallyDelivered };
}

public static class ShiftCodes
{
    public const string Morning = "MORNING";
    public const string Afternoon = "AFTERNOON";
    public const string Overtime = "OVERTIME";

    public static readonly IReadOnlyList<string> All = new[] { Morning, Afternoon, Overtime };
}

// RequestTypes, RequestStatuses: RequestConstants.cs. SettingKeys: AttendanceCodes.cs (một nơi cho mọi luồng).

/// <summary>Tiền tố mã phiếu: TIỀN TỐ-yyyyMMdd-STT.</summary>
public static class DocumentPrefixes
{
    public const string MaterialReceipt = "PN";
    public const string MaterialIssue = "PC";
    public const string WorkOrder = "LC";
    public const string CutHandover = "BG";
    public const string FgHandover = "TP";
    public const string DispatchNote = "XH";
    public const string MaterialReturn = "TV";
    public const string Stocktake = "KK";
    public const string PurchaseOrder = "DM";
}

public static class PurchaseOrderStatuses
{
    public const string PendingFinalisation = "PENDING_FINALISATION";
    public const string Unpaid = "UNPAID";
}
