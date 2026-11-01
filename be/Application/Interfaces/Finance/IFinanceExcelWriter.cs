using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC13 (xuất Excel) cho màn Doanh thu (UC27) và Hòa vốn (UC11) — MSG27.</summary>
public interface IFinanceExcelWriter
{
    /// <summary>Doanh thu — không có cột đơn giá CMT (GBR-02, xem RevenueSummaryDto).</summary>
    byte[] WriteRevenueSheet(IReadOnlyList<RevenueSummaryDto> rows);

    /// <summary>Hòa vốn + giá trị thặng dư — chỉ dùng cho Admin (chứa đơn giá CMT/biến phí/định phí/thặng dư).</summary>
    byte[] WriteBreakEvenSheet(IReadOnlyList<BreakEvenResultDto> rows);
}
