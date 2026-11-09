namespace NX_MTD.Application.DTOs;

/// <summary>Một dòng đọc từ file Excel nhân viên (chưa kiểm hợp lệ nghiệp vụ). Cột: Mã NV | Họ tên | Giới tính | Ngày sinh |
/// SĐT | Vai trò | Mã tổ | Hình thức lương | Ngày vào làm | Mã chấm công.</summary>
public sealed record EmployeeExcelRowDto(int RowNumber, string? Code, string? FullName, string? Gender, DateOnly? DateOfBirth,
    string? Phone, string? Role, string? TeamCode, string? PayType, DateOnly? StartDate, int? AttendanceCode, string? ParseError);

public sealed record EmployeeImportRowDto(int RowNumber, string? Code, string? FullName, string? Role, string? TeamCode,
    string Status, string? Message);

/// <summary>MSG24: {a} dòng hợp lệ, {b} dòng lỗi, {c} dòng trùng. Chỉ các dòng hợp lệ được nhập.</summary>
public sealed record EmployeeImportPreviewDto(int ValidCount, int ErrorCount, int DuplicateCount, string Summary,
    IReadOnlyList<EmployeeImportRowDto> Rows);

/// <summary>Mật khẩu tạm chỉ trả MỘT lần ngay khi nhập (D29), không lưu bản rõ.</summary>
public sealed record EmployeeImportCreatedDto(string Username, string FullName, string TemporaryPassword);

public sealed record EmployeeImportResultDto(int CreatedCount, int FailedCount, IReadOnlyList<EmployeeImportCreatedDto> Created,
    IReadOnlyList<EmployeeImportRowDto> Failed);
