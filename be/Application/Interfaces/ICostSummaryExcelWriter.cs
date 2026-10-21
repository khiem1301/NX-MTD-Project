using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC13 (xuất Excel) cho màn "Tổng hợp chi phí" (UC12, MI-19) — MSG27.</summary>
public interface ICostSummaryExcelWriter
{
    byte[] WriteCostSummarySheet(CostSummaryDto dto);
}
