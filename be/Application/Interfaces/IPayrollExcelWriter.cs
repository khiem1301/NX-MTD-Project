using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>Dựng file Excel xuất dữ liệu tính lương (UC31). Đặt ở Application để Service không phụ thuộc
/// thư viện Excel cụ thể; Infrastructure triển khai (giống IExcelAttendanceParser).</summary>
public interface IPayrollExcelWriter
{
    byte[] WriteExportSheet(string periodMonth, IReadOnlyList<PayrollExportRowDto> rows);
}
