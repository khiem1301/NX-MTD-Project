using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// Đọc file Excel chấm công dự phòng (cột: mã chấm công, ngày, giờ vào, giờ ra).
/// Đặt ở Application để Service không phụ thuộc thư viện Excel cụ thể; Infrastructure triển khai.
/// </summary>
public interface IExcelAttendanceParser
{
    /// <summary>Đọc toàn bộ file vào bộ nhớ. Ném InvalidDataException khi file sai cột / giá trị.</summary>
    IReadOnlyList<AttendanceExcelRowDto> Parse(Stream stream);
}
