using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

// ---- Hồ sơ + đổi mật khẩu (UC03, UC04) ----
/// <summary>UC04: chỉ đọc. Không có kiểu lương, mã chấm công, trạng thái tài khoản.</summary>
public sealed record ProfileDto(int EmployeeId, string EmployeeCode, string FullName, string? Gender, DateOnly? DateOfBirth,
    string? Phone, string? AvatarUrl, string Role, string RoleName, string? TeamName, DateOnly StartDate,
    string? CurrentGradeCode, bool IsTemporaryPassword);

public sealed record ChangePasswordDto(string CurrentPassword, string NewPassword, string ConfirmPassword);

// ---- Nhân viên (UC16, UC17) ----
public sealed record EmployeeQuery : PageQuery
{
    public int? TeamId { get; init; }
    public string? Role { get; init; }     // PascalCase
    public string? Status { get; init; }   // Active / Resigned; null = Active
}

public sealed record EmployeeListItemDto(int EmployeeId, string EmployeeCode, string FullName, string Role, string RoleName,
    int? TeamId, string? TeamName, string PayType, string? CurrentGradeCode, int? AttendanceCode, string Status,
    string? AccountStatus, DateOnly StartDate);

public sealed record AccountInfoDto(int AccountId, string Username, string Status, bool IsTemporaryPassword,
    DateTime? LockedAt, string? LockReason);

public sealed record EmployeeDetailDto
{
    public int EmployeeId { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Gender { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Phone { get; init; }
    public string? AvatarUrl { get; init; }
    public string Role { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public int? TeamId { get; init; }
    public string? TeamName { get; init; }
    public string PayType { get; init; } = string.Empty;
    public string? CurrentGradeCode { get; init; }
    public int? AttendanceCode { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? ResignDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public byte[] RowVer { get; init; } = Array.Empty<byte>();

    /// <summary>Chỉ Admin; Quản đốc nhận null.</summary>
    public AccountInfoDto? Account { get; init; }
    public IReadOnlyList<AuditLogDto> History { get; init; } = Array.Empty<AuditLogDto>();
}

/// <param name="Role">PascalCase: Worker, TeamLeader...</param>
/// <param name="PayType">PascalCase: PieceRate, Daily, Monthly.</param>
public sealed record CreateEmployeeDto(string EmployeeCode, string FullName, string? Gender, DateOnly? DateOfBirth, string? Phone,
    string Role, int? TeamId, string PayType, DateOnly StartDate, int? AttendanceCode);

/// <summary>Trả MỘT lần (D29). Không log, không lưu bản rõ.</summary>
public sealed record CreatedEmployeeDto(int EmployeeId, string Username, string TemporaryPassword);
public sealed record TemporaryPasswordDto(string Username, string TemporaryPassword);

public sealed record AdminUpdateEmployeeDto(string FullName, string? Gender, DateOnly? DateOfBirth, string? Phone,
    string Role, int? TeamId, string PayType, DateOnly StartDate, int? AttendanceCode, byte[] RowVer);

/// <summary>UC16: Quản đốc không đổi vai trò, tổ (dùng chuyển tổ), kiểu lương.</summary>
public sealed record ManagerUpdateEmployeeDto(string FullName, string? Gender, DateOnly? DateOfBirth, string? Phone,
    DateOnly StartDate, int? AttendanceCode, byte[] RowVer);

public sealed record ResignEmployeeDto(DateOnly ResignDate, string Reason, byte[] RowVer);
public sealed record LockAccountDto(string Reason);

// ---- Tổ (UC18) ----
public sealed record TeamListItemDto(int TeamId, string TeamCode, string TeamName, string TeamType, string Status,
    string? LeaderName, string? DeputyName, int MemberCount);

public sealed record TeamMemberDto(int EmployeeId, string EmployeeCode, string FullName, string Role, string RoleName,
    string PayType, string? CurrentGradeCode);

public sealed record TeamDetailDto(int TeamId, string TeamCode, string TeamName, string TeamType, string Status,
    IReadOnlyList<TeamMemberDto> Members, IReadOnlyList<AuditLogDto> History);

/// <param name="TeamType">PascalCase: Cutting, Sewing, Office.</param>
public sealed record SaveTeamDto(string TeamCode, string TeamName, string TeamType);
public sealed record TransferMembersDto(IReadOnlyList<int> EmployeeIds, DateOnly EffectiveDate, string? Reason);

/// <param name="Position">Leader hoặc Deputy.</param>
public sealed record AppointDto(int EmployeeId, string Position);

// ---- Tra cứu dùng chung (Minh, Kiều) ----
public sealed record TeamLookupDto(int TeamId, string TeamCode, string TeamName, string TeamType);
public sealed record EmployeeLookupDto(int EmployeeId, string EmployeeCode, string FullName, string Role, int? TeamId, string PayType);
