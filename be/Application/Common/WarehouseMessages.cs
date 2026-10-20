namespace NX_MTD.Application.Common;

/// <summary>Chuỗi tiếng Việt của luồng kho / cắt (Thái).</summary>
public static class WarehouseMessages
{
    public const string NotFound = "Không tìm thấy dữ liệu.";
    public const string InsufficientStock = "Tồn kho không đủ";
    public const string InvalidStatus = "Phiếu không ở trạng thái cho phép thao tác này.";
    public const string OrderClosed = "Đơn hàng đã đóng, không thể lập thêm phiếu.";
    public const string TeamMismatch = "Phiếu không thuộc tổ của bạn.";
    public const string ReasonRequired = "Vui lòng nhập lý do.";
    public const string DiscrepancyReasonRequired = "Số xác nhận khác số giao, vui lòng nhập lý do.";
    public const string ConfirmedQtyExceedsIssued = "Số xác nhận không được lớn hơn số đã cấp.";
    public const string DuplicateLine = "Một dòng bị khai báo nhiều lần.";
    public const string LinesRequired = "Phiếu phải có ít nhất một dòng.";
    public const string QtyMustBePositive = "Số lượng phải lớn hơn 0.";
    public const string QtyMustNotBeNegative = "Số lượng không được âm.";

    public const string MaterialCodeDuplicate = "Mã NVL đã tồn tại.";
    public const string MaterialNameDuplicate = "Tên NVL đã tồn tại trong cùng loại.";
    public const string MaterialCategoryInvalid = "Nhóm NVL không khớp với loại.";
    public const string MaterialInactive = "NVL đã ngừng sử dụng.";
    public const string MaterialStatusChanged = "Đã cập nhật trạng thái NVL.";

    public const string OrderMaterialDuplicate = "NVL và màu này đã có trong đơn.";
    public const string OrderMaterialInUse = "NVL của đơn đã có phiếu nhập, không thể xóa.";
    public const string CustomerDateRequired = "Khách có cấp NVL thì phải nhập ngày dự kiến giao.";
    public const string NormProductNotInOrder = "Mã hàng của định mức không có trong đơn.";
    public const string OrderMaterialNotInOrder = "NVL không thuộc đơn hàng của phiếu.";
    public const string OrderLineNotInOrder = "Dòng đơn không thuộc đơn hàng.";

    public const string ReceiptConfirmed = "Đã xác nhận phiếu nhập, tồn kho NVL đã tăng.";
    public const string ReceiptReturned = "Đã trả phiếu nhập cho Thủ kho.";
    public const string SupplierSourceNotSupported = "Phiếu nhập NVL từ nhà cung cấp làm ở Phase 2 (TH-12).";

    public const string CuttingTeamInvalid = "Tổ nhận phải là tổ cắt đang hoạt động.";
    public const string SewingTeamInvalid = "Tổ nhận phải là tổ may đang hoạt động.";
    public const string ProductNotInOrder = "Mã hàng không có trong đơn.";
    public const string FabricShortage = "Không đủ vải cho lệnh cắt";
    public const string PlanExceedsOrder = "Tổng số cắt theo kế hoạch vượt số lượng đặt";
    public const string WorkOrderHasActivity = "Lệnh cắt đã có bàn cắt hoặc phiếu cấp, không thể sửa / hủy.";
    public const string WorkOrderCancelled = "Đã hủy lệnh cắt.";

    public const string IssueReceived = "Đã xác nhận nhận hàng, tồn kho đã giảm.";
    public const string IssueRejected = "Đã từ chối phiếu cấp, số giữ chỗ đã được nhả.";
    public const string RequestInvalid = "Đơn xin cấp không hợp lệ (sai loại, chưa duyệt hoặc khác đơn hàng).";
    public const string WorkOrderRequiredForMaterial = "Cấp NVL cho tổ cắt phải theo lệnh cắt.";
    public const string WorkOrderNotIssuable = "Lệnh cắt không còn mở để cấp vải.";

    public const string LayQtyExceedsPlan = "Số cắt vượt số kế hoạch + % dư cho phép";
    public const string LayQtyOverPlanWarning = "Đã lưu. Lưu ý: số cắt vượt số kế hoạch (vẫn trong % dư cho phép).";
    public const string LayLocked = "Bàn cắt đã bàn giao, không thể sửa.";
    public const string LayOrderLineNotInWorkOrder = "Dòng đơn không thuộc lệnh cắt.";
    public const string WorkOrderNotCuttable = "Lệnh cắt đã hoàn thành hoặc đã hủy.";

    public const string HandoverExceedsCut = "Số bàn giao vượt số đã cắt chưa bàn giao";
    public const string HandoverConfirmed = "Đã nhận BTP vào kho.";
    public const string HandoverRejected = "Đã từ chối phiếu bàn giao.";
}
