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
    public const string Cancelled = "CANCELLED";
    public const string Stopped = "STOPPED";
    public const string Delivered = "DELIVERED";
    public const string Completed = "COMPLETED";

    /// <summary>Đơn đã đóng thì không lập thêm phiếu kho / lệnh cắt.</summary>
    public static readonly IReadOnlyList<string> Closed = new[] { Cancelled, Stopped, Delivered, Completed };
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

public static class ShiftCodes
{
    public const string Morning = "MORNING";
    public const string Afternoon = "AFTERNOON";
    public const string Overtime = "OVERTIME";

    public static readonly IReadOnlyList<string> All = new[] { Morning, Afternoon, Overtime };
}

// RequestTypes và SettingKeys khai báo chung ở AttendanceCodes.cs (đã gộp hằng của cả hai luồng).

public static class RequestStatuses
{
    public const string Approved = "APPROVED";
}

/// <summary>Tiền tố mã phiếu: TIỀN TỐ-yyyyMMdd-STT.</summary>
public static class DocumentPrefixes
{
    public const string MaterialReceipt = "PN";
    public const string MaterialIssue = "PC";
    public const string WorkOrder = "LC";
    public const string CutHandover = "BG";
    public const string PurchaseOrder = "DM";
}

public static class PurchaseOrderStatuses
{
    public const string PendingFinalisation = "PENDING_FINALISATION";
    public const string Unpaid = "UNPAID";
}
