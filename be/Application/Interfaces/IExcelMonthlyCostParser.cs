using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// Đọc file Excel chi phí tháng do Kế toán tải lên (DEV-MI-12-3): mỗi dòng là một khoản chi phí
/// (cột: loại chi phí, kiểu FIXED/VARIABLE, mã hàng, số tiền, số lượng, ghi chú).
/// Giống <see cref="IExcelPayrollParser"/>: không ném lỗi theo từng dòng dữ liệu (chỉ ném khi cả
/// file sai cấu trúc — thiếu sheet/sai cột), để Service tự dựng preview đủ 3 loại hợp lệ/lỗi/trùng (MSG24).
/// </summary>
public interface IExcelMonthlyCostParser
{
    /// <summary>Đọc toàn bộ file vào bộ nhớ. Ném InvalidDataException khi file không có sheet hoặc sai cột.</summary>
    IReadOnlyList<MonthlyCostExcelRowDto> Parse(Stream stream);
}
