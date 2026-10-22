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
    public const string SourceInvalid = "Nguồn nhập phải là Khách cấp hoặc Nhà cung cấp.";
    public const string PurchaseOrderRequired = "Phiếu nhập từ nhà cung cấp phải chọn đơn mua.";
    public const string OrderRequired = "Phiếu nhập NVL khách cấp phải chọn đơn hàng.";
    public const string PurchaseOrderNotReceivable = "Đơn mua chưa đặt hàng hoặc đã nhận đủ / đã đóng, không lập thêm phiếu nhập.";
    public const string PurchaseLineNotInOrder = "Dòng đơn mua không thuộc đơn mua của phiếu.";
    public const string ReceiptLineTarget = "Mỗi dòng phiếu nhập phải chọn NVL của đơn (khách cấp) hoặc dòng đơn mua (nhà cung cấp).";
    public const string ReceiptExceedsPurchase = "Số nhập vượt số còn phải nhận của dòng đơn mua";
    public const string ShortDeliveryCreated = "Phiếu nhập có hàng thiếu hoặc lỗi. Hệ thống đã tạo yêu cầu xử lý thiếu NVL.";
    public const string ShortageNotResolvable = "Yêu cầu này không còn chờ xử lý thiếu NVL.";
    public const string ShortageResolutionInvalid = "Cách xử lý phải là giao bù hoặc không bù.";
    public const string ShortageResolved = "Đã ghi nhận cách xử lý hàng thiếu.";
    public const string ShortageNotReplenish = "Chưa chọn giao bù cho yêu cầu này, không lập được phiếu bổ sung.";
    public const string ReplenishExceedsShortage = "Số giao bù vượt số thiếu / lỗi của dòng";
    public const string SupplementaryExceedsReplenish = "Số nhận bù vượt số còn phải bù của dòng";

    public const string PurchaseOrderMaterialNotInOrder = "NVL và màu này chưa khai báo trong đơn hàng đã chọn (UC09).";
    public const string PurchaseOrderCreated = "Đã tạo đơn mua NVL.";

    public const string CuttingTeamInvalid = "Tổ nhận phải là tổ cắt đang hoạt động.";
    public const string SewingTeamInvalid = "Tổ nhận phải là tổ may đang hoạt động.";
    public const string ProductNotInOrder = "Mã hàng không có trong đơn.";
    public const string FabricShortage = "Không đủ vải cho lệnh cắt";
    public const string NoFabricNorm = "Mã hàng chưa có định mức vải trong NVL của đơn (UC09), không tính được vải cần.";
    public const string PlanExceedsOrder = "Tổng số cắt theo kế hoạch vượt số lượng đặt";
    public const string WorkOrderHasActivity = "Lệnh cắt đã có bàn cắt hoặc phiếu cấp, không thể sửa / hủy.";
    public const string WorkOrderCancelled = "Đã hủy lệnh cắt.";

    public const string IssueReceived = "Đã xác nhận nhận hàng, tồn kho đã giảm.";
    public const string IssueRejected = "Đã từ chối phiếu cấp, số giữ chỗ đã được nhả.";
    public const string RequestInvalid = "Đơn xin cấp không hợp lệ (sai loại, đã xử lý, khác tổ hoặc khác đơn hàng). Cấp BTP cho tổ may bắt buộc có đơn Xin BTP.";
    public const string WorkOrderRequiredForMaterial = "Cấp NVL cho tổ cắt phải theo lệnh cắt.";
    public const string WorkOrderNotIssuable = "Lệnh cắt không còn mở để cấp vải.";
    public const string MaterialNotInWorkOrder = "Vải không có trong định mức của lệnh cắt.";

    public const string LayQtyExceedsPlan = "Số cắt vượt số kế hoạch + % dư cho phép";
    public const string LayQtyOverPlanWarning = "Đã lưu. Lưu ý: số cắt vượt số kế hoạch (vẫn trong % dư cho phép).";
    public const string LayLocked = "Bàn cắt đã bàn giao, không thể sửa.";
    public const string LayOrderLineNotInWorkOrder = "Dòng đơn không thuộc lệnh cắt.";
    public const string WorkOrderNotCuttable = "Lệnh cắt đã hoàn thành hoặc đã hủy.";

    public const string HandoverExceedsCut = "Số bàn giao vượt số đã cắt chưa bàn giao";
    public const string HandoverConfirmed = "Đã nhận BTP vào kho.";
    public const string HandoverConfirmedExceedsHanded = "Số xác nhận không được lớn hơn số tổ giao.";
    public const string HandoverRejected = "Đã từ chối phiếu bàn giao.";

    public const string FgHandoverExceedsQc = "Số bàn giao vượt số QC đạt chưa bàn giao";
    public const string FgLineNotInOrder = "Dòng đơn không thuộc đơn hàng / mã hàng của phiếu bàn giao.";
    public const string FgHandoverCreated = "Đã lập phiếu bàn giao thành phẩm, chờ Thủ kho nhận.";
    public const string FgHandoverConfirmed = "Đã nhận thành phẩm vào kho.";
    public const string FgHandoverRejected = "Đã từ chối phiếu bàn giao thành phẩm.";
    public const string FgHandoverCancelled = "Đã rút phiếu bàn giao thành phẩm.";

    public const string DispatchExceedsOrder = "Số xuất vượt số còn phải giao của đơn";
    public const string DispatchLineNotInOrder = "Dòng đơn không thuộc đơn hàng của phiếu xuất.";
    public const string DispatchCreated = "Đã lập phiếu xuất hàng, thành phẩm đã được giữ chỗ.";
    public const string DispatchCancelled = "Đã hủy phiếu xuất hàng, số giữ chỗ đã được nhả.";
    public const string DispatchExported = "Đã xuất hàng, tồn kho thành phẩm đã giảm.";
    public const string DispatchRejected = "Đã từ chối phiếu xuất hàng, số giữ chỗ đã được nhả.";
    public const string DispatchDelivered = "Đã xác nhận khách nhận hàng.";
    public const string DispatchReceivedExceedsIssued = "Số khách nhận không được lớn hơn số đã xuất.";
    public const string DeliveryDateRequired = "Vui lòng chọn ngày giao hàng.";
    public const string DeliveryAddressRequired = "Vui lòng nhập địa chỉ giao hàng.";
    public const string CustomerReceivedDateRequired = "Vui lòng nhập ngày khách nhận hàng.";
    public const string DispatchReceivedDateInvalid = "Ngày khách nhận không được trước ngày xuất hàng.";
}
