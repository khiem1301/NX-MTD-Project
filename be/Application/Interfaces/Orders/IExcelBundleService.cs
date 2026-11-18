using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>DEV-ALL-122: đọc mọi bảng của file Excel nhiều sheet và kiểm bản đã sửa trên popup (không ghi gì).</summary>
public interface IExcelBundleService
{
    /// <summary>Đọc mọi bảng nhận được; file không có bảng nào thì lỗi.</summary>
    Result<ExcelBundleDto> Parse(Stream stream);

    /// <summary>Kiểm bản đã sửa: mã hàng, định mức (Pending khi thiếu màu), NVL của đơn (Pending khi chưa có đơn).</summary>
    Task<Result<ExcelBundleCheckResultDto>> CheckAsync(ExcelBundleCheckDto dto, CancellationToken ct);
}
