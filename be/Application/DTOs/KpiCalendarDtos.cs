namespace NX_MTD.Application.DTOs;

/// <summary>
/// Một ngày trên lịch KPI. TargetQty = KPI đã đặt cho ngày đó (0 = chưa đặt). BtpRemaining = bán thành phẩm còn trong kho sau ngày đó:
/// ngày đã qua / hôm nay là số thật (cắt lũy kế − ra chuyền lũy kế của mọi tổ), ngày tương lai là dự báo nếu mọi KPI đã đặt đều đạt.
/// ActualQty = hàng hoàn thiện QA/QC nhập cho tổ (null ở ngày tương lai).
/// </summary>
public sealed record KpiCalendarDayDto(
    DateOnly WorkDate, bool IsFuture, bool IsToday, int TargetQty, int CumTarget, int CutQty, int? ActualQty,
    int BtpRemaining, bool LowBtp, bool BehindTarget);

/// <summary>
/// Tổng BTP = BTP đã cắt vào kho − hàng QA/QC đã nhập ra chuyền (đơn + mã hàng, mọi tổ, đến hôm nay).
/// CommittedAll = KPI đã đặt mà chưa ra chuyền của mọi KPI đang chạy cùng đơn + mã hàng (các ngày sau hôm nay + phần còn thiếu của hôm nay).
/// RemainingToSet = TotalBtp − CommittedAll: số còn lại để có thể đặt thêm KPI (âm = đã đặt vượt BTP).
/// CommittedThis = phần của riêng KPI này trong CommittedAll (để FE tính lại ngay khi đang sửa bản nháp).
/// </summary>
public sealed record KpiCalendarSummaryDto(
    DateOnly Today, int PlanTotal, int Allocated, int Unallocated, int CutTotal, int OutTotal, int TotalBtp,
    int CommittedAll, int CommittedThis, int RemainingToSet, bool CanEdit);

public sealed record KpiCalendarDto(
    int KpiTargetId, string OrderCode, string ProductCode, string ProductName, string SewingTeamName,
    DateOnly StartDate, DateOnly EndDate, string Status, KpiCalendarSummaryDto Summary,
    IReadOnlyList<KpiCalendarDayDto> Days);
