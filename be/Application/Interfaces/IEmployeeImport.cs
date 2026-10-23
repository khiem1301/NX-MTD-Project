using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>Đọc file Excel nhân viên (UC17 import). Không ném lỗi theo từng dòng; chỉ ném InvalidDataException khi sai cấu trúc.</summary>
public interface IExcelEmployeeParser
{
    IReadOnlyList<EmployeeExcelRowDto> Parse(Stream stream);
}

public interface IEmployeeImportService
{
    Task<Result<EmployeeImportPreviewDto>> PreviewAsync(Stream stream, CancellationToken ct);
    Task<Result<EmployeeImportResultDto>> ImportAsync(Stream stream, CancellationToken ct);
}

/// <summary>UC22: Admin xem nhật ký thay đổi (AuditLog) của toàn hệ thống.</summary>
public interface IAuditLogQueryService
{
    Task<Result<PagedResult<AuditLogRowDto>>> ListAsync(AuditLogQuery query, CancellationToken ct);
    Task<Result<IReadOnlyList<string>>> GetTableNamesAsync(CancellationToken ct);
}
