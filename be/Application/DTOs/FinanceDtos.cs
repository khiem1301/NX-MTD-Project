using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

// ---------- Doanh thu (UC27, GBR-02: Kế toán + Admin — KHÔNG có CmtUnitPrice/đơn giá CMT) ----------

/// <summary>Doanh thu theo tháng/đơn/mã hàng, chiếu từ vw_MonthlyRevenue (SL hoàn thiện × CmtUnitPrice đã
/// tính sẵn trong view). Không có trường đơn giá CMT — đó là dữ liệu "mật", chỉ Admin được xem (GBR-02),
/// và chỉ lộ ra ở màn hòa vốn (<see cref="BreakEvenResultDto"/>), không phải màn doanh thu.</summary>
public sealed record RevenueSummaryDto(
    string PeriodMonth, int OrderId, string OrderCode, int ProductId, string ProductCode, string ProductName,
    int FinishedQty, decimal Revenue);

public sealed record RevenueQuery : PageQuery
{
    public string? PeriodMonth { get; init; }
    public int? OrderId { get; init; }
    public int? ProductId { get; init; }
}

// ---------- Hòa vốn + giá trị thặng dư (UC11, GBR-17 — chỉ Admin) ----------

/// <summary>Yêu cầu chạy một lần tính hòa vốn cho một tháng; Admin chọn cách phân bổ định phí mỗi lần
/// chạy (OUTPUT hoặc LABOUR_HOURS) — không có giá trị mặc định cố định, cả hai công thức đều cài đặt.</summary>
public sealed record BreakEvenCalculateDto(string PeriodMonth, string AllocationBasis);

/// <summary>Một dòng kết quả hòa vốn theo mã hàng — CHỈ Admin xem được (chứa CmtUnitPrice/VariableCost/
/// AllocatedFixedCost/SurplusValue, đều là dữ liệu "mật" theo GBR-02).</summary>
public sealed record BreakEvenResultDto(
    int BreakEvenResultId, int PeriodId, string PeriodMonth, int ProductId, string ProductCode, string ProductName,
    string AllocationBasis, int FinishedQty, decimal CmtUnitPrice, decimal Revenue, decimal VariableCost,
    decimal AllocatedFixedCost, decimal? BreakEvenQty, decimal? PercentReached, decimal SurplusValue, bool IsAtRisk,
    DateTime CalculatedAt, string? AtRiskWarning);

/// <summary>Kết quả sau khi Admin bấm "Tính toán": toàn bộ dòng của lần chạy vừa rồi + danh sách cảnh báo
/// MSG26 cho các mã hàng nguy cơ lỗ (để FE hiện ngay, không phải tự dựng lại câu chữ).</summary>
public sealed record BreakEvenCalculationResultDto(
    int PeriodId, string PeriodMonth, string AllocationBasis, DateTime CalculatedAt,
    IReadOnlyList<BreakEvenResultDto> Items, IReadOnlyList<string> AtRiskWarnings);

/// <summary>Xem lại kết quả đã tính: mặc định (History = false) chỉ lấy lần tính gần nhất của tháng
/// (cùng CalculatedAt); History = true trả toàn bộ lịch sử các lần tính (BreakEvenResult không bao giờ bị
/// ghi đè, UNIQUE (PeriodId, CalculatedAt, ProductId) — xem comment schema).</summary>
public sealed record BreakEvenListResultDto(
    int PeriodId, string PeriodMonth, IReadOnlyList<BreakEvenResultDto> Items);
