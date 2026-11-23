namespace NX_MTD.Application.DTOs;

// DEV-ALL-170: trang chủ theo vai trò. Mỗi role một API GET /api/v1/<role>/home trả đủ số liệu trong một lần gọi.

/// <summary>Tiền tháng này và tháng trước (null = tháng đó chưa có số liệu, vd chưa nhập chi phí).</summary>
public sealed record HomeMoneyDto(decimal? ThisMonth, decimal? LastMonth);

/// <summary>Giám đốc: việc cần quyết, số liệu tháng và tiến độ các đơn đang sản xuất / giao dở.</summary>
public sealed record AdminHomeDto(
    int PieceRatesPending, int PayrollsPending, int OrdersRunning, HomeMoneyDto Revenue, HomeMoneyDto Cost,
    IReadOnlyList<OrderProgressDto> Orders);

/// <summary>
/// Quản đốc: việc chờ xử lý, tiến độ các đơn còn mở (gồm đơn chờ NVL) và đơn từ chờ duyệt mới nhất.
/// <c>LeaderAbsentTeams</c>: mã tổ có tổ trưởng chưa có giờ vào sau giờ kiểm (LEADER_ABSENT_CHECK_TIME, mặc định 08:00).
/// </summary>
public sealed record ManagerHomeDto(
    int ReceiptsPending, int RequestsPending, IReadOnlyList<string> LeaderAbsentTeams,
    IReadOnlyList<OrderProgressDto> Orders, IReadOnlyList<RequestListItemDto> Requests);

/// <summary>Một dòng trong danh sách của trang chủ; <c>TagColor</c>: red | orange | blue | green | gray.</summary>
public sealed record HomeItemDto(string Title, string? Meta, string Tag, string TagColor);

/// <summary>
/// Kế toán: lượt thiếu chấm công tháng này, mã hàng thiếu / bị trả đơn giá, đơn mua chưa thanh toán đủ, đơn hàng chưa chốt giá
/// trị; tình trạng tháng (chi phí đã nhập chưa, trạng thái bảng lương; null = chưa có bảng lương).
/// </summary>
public sealed record AccountantHomeDto(
    int MissingPunch, int PieceRatesMissing, int PieceRatesRejected, int PurchasesUnpaid, int OrdersNotSet,
    bool CostEntered, string? PayrollStatus, IReadOnlyList<HomeItemDto> Orders, IReadOnlyList<HomeItemDto> Purchases);

/// <summary>Thủ kho: NVL chờ nhận, lệnh cắt chưa cấp vải, đơn xin chờ cấp, bàn giao chờ nhận (BTP + thành phẩm + trả NVL).</summary>
public sealed record WarehouseHomeDto(
    int Incoming, int WorkOrdersNoIssue, int RequestsToIssue, int CutHandovers, int FgHandovers, int Returns,
    IReadOnlyList<HomeItemDto> IncomingItems, IReadOnlyList<HomeItemDto> PendingItems);

/// <summary>Lệnh cắt đang mở của tổ cắt: kế hoạch và đã cắt (cộng theo dòng đơn).</summary>
public sealed record HomeWorkOrderDto(int WorkOrderId, string Code, string OrderCode, string Status, int PlannedQty, int CutQty);

/// <summary>Tổ cắt: phiếu cấp vải chờ nhận, đã cắt chưa bàn giao BTP, lệnh đã xong còn chưa trả vải thừa.</summary>
public sealed record CuttingHomeDto(
    int IssuesPending, int CutNotHanded, int LeftoverNotReturned, IReadOnlyList<HomeWorkOrderDto> WorkOrders);

/// <summary>
/// Tổ may: KPI hôm nay (mục tiêu / đã nhập), phiếu BTP chờ nhận (dòng chặn nhận có <c>TagColor</c> red), công đoạn chưa phân công
/// (chặn nhận BTP), giao việc chờ công nhân xác nhận, suất ăn của tổ hôm nay.
/// </summary>
public sealed record SewingHomeDto(
    decimal KpiTarget, int KpiActual, int BtpPending, int UnassignedOperations, int AllocationsPending,
    int MealSuggested, int? MealConfirmed, bool MealLocked, IReadOnlyList<HomeItemDto> BtpIssues);

/// <summary>Tổ trưởng / tổ phó: <c>TeamType</c> là CUTTING hoặc SEWING, chỉ khối tương ứng có dữ liệu.</summary>
public sealed record TeamLeaderHomeDto(string? TeamType, CuttingHomeDto? Cutting, SewingHomeDto? Sewing);

/// <summary>QA/QC: hàng trên chuyền chưa kiểm, hoàn thiện chưa nhập kho thành phẩm, bàn giao thành phẩm chờ kho nhận.</summary>
public sealed record QaqcHomeDto(
    int OnLineQty, int NotReceivedQty, int HandoversPending, IReadOnlyList<HomeItemDto> Lines,
    IReadOnlyList<HomeItemDto> Handovers);

/// <summary>Phiếu lương đã duyệt mới nhất của công nhân.</summary>
public sealed record HomePayslipDto(string PeriodMonth, decimal NetPay);

/// <summary>Công nhân: lương tạm tính hôm nay và từ đầu tháng, phiếu lương mới nhất, việc chờ xác nhận, phiếu lỗi 7 ngày, suất ăn hôm nay.</summary>
public sealed record WorkerHomeDto(
    decimal? EstimatedToday, decimal? EstimatedMonth, HomePayslipDto? LatestPayslip, int AllocationsPending,
    IReadOnlyList<HomeItemDto> Allocations, IReadOnlyList<HomeItemDto> Defects,
    bool MealHasAttendance, bool MealWillEat, bool MealLocked);

/// <summary>Cơ điện: đơn từ do mình tạo còn mở, hiện như ticket (xem, không có thao tác đóng).</summary>
public sealed record HomeTicketDto(int RequestId, string Code, string RequestType, string Status, string? Title, DateTime CreatedAt);

public sealed record ElectricianHomeDto(IReadOnlyList<HomeTicketDto> Tickets);
