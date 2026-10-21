using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// Đọc file "Bảng thanh toán lương" Kế toán đã tính xong ngoài hệ thống (DEV-MI-09-4): nhiều sheet (mỗi tổ một
/// sheet), mỗi dòng một nhân viên, cột nhận theo TÊN tiêu đề (không theo vị trí) vì mỗi tổ có bộ cột khác nhau.
/// Khác với <see cref="IExcelAttendanceParser"/>: không ném lỗi theo từng dòng dữ liệu (chỉ ném khi cả
/// file sai cấu trúc — không có sheet nào đúng mẫu), để Service tự dựng preview đủ 3 loại hợp lệ/lỗi/trùng (MSG24).
/// </summary>
public interface IExcelPayrollParser
{
    /// <summary>Đọc toàn bộ file vào bộ nhớ. Ném InvalidDataException khi không có sheet nào đúng mẫu.</summary>
    IReadOnlyList<PayrollExcelRowDto> Parse(Stream stream);
}
