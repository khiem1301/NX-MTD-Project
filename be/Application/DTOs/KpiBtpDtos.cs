namespace NX_MTD.Application.DTOs;

/// <summary>Một KPI chọn được trên màn so sánh BTP – KPI.</summary>
public sealed record KpiBtpOptionDto(
    int KpiTargetId, string OrderCode, string ProductCode, string ProductName, string SewingTeamName,
    DateOnly StartDate, DateOnly EndDate, string Status);

/// <summary>
/// Một ngày của bảng so sánh. Lũy kế tính từ đầu kỳ KPI; BtpRemaining = BTP còn trong kho sau ngày đó
/// (số dư đầu kỳ + cắt lũy kế − thực tế lũy kế). Ngày tương lai: ActualQty = null, BtpRemaining là dự báo nếu tổ đạt đúng KPI.
/// CumCut / CumActual đã gồm số dư trước đầu kỳ.
/// </summary>
public sealed record KpiBtpDayDto(
    DateOnly WorkDate, bool IsFuture, decimal DailyTarget, decimal CumTarget, int CutQty, int CumCut,
    int? ActualQty, int CumActual, decimal BtpRemaining, decimal CutShortfall, bool LowBtp, bool BehindTarget);

public sealed record KpiBtpSummaryDto(
    int PlanTotal, int OpeningBtp, int CutTotal, int ActualTotal, decimal BtpRemainingNow, decimal CutShortfall);

public sealed record KpiBtpComparisonDto(KpiBtpOptionDto Target, KpiBtpSummaryDto Summary, IReadOnlyList<KpiBtpDayDto> Days);
