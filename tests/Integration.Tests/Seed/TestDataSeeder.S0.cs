using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

/// <summary>
/// Dữ liệu nền S0 của IT_B1 (Phần 0): 16 nhân viên, 5 tổ, bậc thợ B1/B2/B3/B9, GradeLevel GL1–GL3, RJ snapshots.
/// Chuỗi trạng thái đối chiếu với Domain: Account khóa = <see cref="RecordStatus.Locked"/> ("LOCKED"),
/// Team ngừng = <see cref="RecordStatus.Discontinued"/> ("DISCONTINUED"),
/// GradeLevelCode ngừng = <see cref="GradeCodes.Inactive"/> ("INACTIVE").
/// </summary>
public sealed partial class TestDataSeeder
{
    private readonly S0Context _s0 = new();

    /// <summary>
    /// Nền tối thiểu cho IT_B1: chỉ Role + Team + Catalog + Orders (đủ cho Lookups 041–043), không seed People/Finance
    /// để dữ liệu nhân sự chỉ gồm đúng 16 người của S0.
    /// </summary>
    public async Task<TestContext> SeedBatch1BaseAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync(ct);
        // Chỉ 2 tổ may mà seed đơn hàng (OrderLineTeam) cần; không seed TEAM-CUT/OFFICE/SEW-X để IT_B1_119 ("chỉ T_CUT01") đúng với S0.
        _ctx.TeamSewA = await UpsertTeamAsync("TEAM-SEW-A", TeamTypes.Sewing, RecordStatus.Active, ct);
        _ctx.TeamSewB = await UpsertTeamAsync("TEAM-SEW-B", TeamTypes.Sewing, RecordStatus.Active, ct);
        await SeedCatalogAsync(ct);
        await SeedOrdersAsync(ct);
        return _ctx;
    }

    /// <summary>Chế độ đồng thời (commit thật): chỉ đảm bảo Role tồn tại; trả danh sách RoleCode do chính lần này tạo để dọn.</summary>
    public async Task<List<string>> SeedRolesForConcurrentAsync(CancellationToken ct = default)
    {
        var existing = await _db.Roles.AsNoTracking().Select(r => r.RoleCode).ToListAsync(ct);
        await SeedRolesAsync(ct);
        return _ctx.Roles.Keys.Where(k => !existing.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>Chạy sau <see cref="SeedBatch1BaseAsync"/> (hoặc <see cref="SeedAsync"/>) (cần Role đã có). Mọi dòng nằm trong ambient transaction nên được Rollback.</summary>
    public async Task<S0Context> SeedS0Async(CancellationToken ct = default)
    {
        var hash = _hasher.Hash(S0Context.Password);

        // --- Team ---
        _s0.TSew01 = await UpsertTeamAsync("T_SEW01", TeamTypes.Sewing, RecordStatus.Active, ct);
        _s0.TSew02 = await UpsertTeamAsync("T_SEW02", TeamTypes.Sewing, RecordStatus.Active, ct);
        _s0.TCut01 = await UpsertTeamAsync("T_CUT01", TeamTypes.Cutting, RecordStatus.Active, ct);
        _s0.TEmpty = await UpsertTeamAsync("T_EMPTY", TeamTypes.Sewing, RecordStatus.Active, ct);
        _s0.TOld = await UpsertTeamAsync("T_OLD", TeamTypes.Sewing, RecordStatus.Discontinued, ct);

        // --- GradeLevelCode (B1 phải có trước vì E_W1.CurrentGradeCode là FK) ---
        _s0.B1 = await UpsertGradeCodeAsync("B1", GradeCodes.Active, ct);
        _s0.B2 = await UpsertGradeCodeAsync("B2", GradeCodes.Active, ct);
        _s0.B3 = await UpsertGradeCodeAsync("B3", GradeCodes.Active, ct);
        _s0.B9 = await UpsertGradeCodeAsync("B9", GradeCodes.Inactive, ct);

        // --- 16 nhân viên + tài khoản ---
        _s0.Adm = await S0PersonAsync("admin01", "S0-ADM01", "Quản Trị Một", RoleCodes.Admin, PayTypes.Monthly, null, null, hash, ct);
        _s0.Adm2 = await S0PersonAsync("admin02", "S0-ADM02", "Quản Trị Hai", RoleCodes.Admin, PayTypes.Monthly, null, null, hash, ct);
        _s0.Mgr = await S0PersonAsync("mgr01", "S0-MGR01", "Quản Đốc Một", RoleCodes.Manager, PayTypes.Monthly, null, null, hash, ct);
        _s0.Acc = await S0PersonAsync("acc01", "S0-ACC01", "Kế Toán Một", RoleCodes.Accountant, PayTypes.Monthly, null, null, hash, ct);
        _s0.Wh = await S0PersonAsync("wh01", "S0-WH01", "Thủ Kho Một", RoleCodes.WarehouseKeeper, PayTypes.Monthly, null, null, hash, ct);
        _s0.Tl1 = await S0PersonAsync("tl01", "S0-TL01", "Tổ Trưởng Một", RoleCodes.TeamLeader, PayTypes.Monthly, _s0.TSew01.TeamId, 1101, hash, ct);
        _s0.Td1 = await S0PersonAsync("td01", "S0-TD01", "Tổ Phó Một", RoleCodes.TeamDeputy, PayTypes.Monthly, _s0.TSew01.TeamId, 1102, hash, ct);
        _s0.W1 = await S0PersonAsync("wk01", "S0-WK01", "Công Nhân Một", RoleCodes.Worker, PayTypes.PieceRate, _s0.TSew01.TeamId, 1001, hash, ct,
            gradeCode: "B1");
        _s0.W2 = await S0PersonAsync("wk02", "S0-WK02", "Công Nhân Hai", RoleCodes.Worker, PayTypes.PieceRate, _s0.TSew02.TeamId, 1002, hash, ct);
        _s0.W5 = await S0PersonAsync("wk05", "S0-WK05", "Công Nhân Năm", RoleCodes.Worker, PayTypes.PieceRate, _s0.TSew02.TeamId, 1005, hash, ct);
        _s0.W3 = await S0PersonAsync("wk03", "S0-WK03", "Công Nhân Ba", RoleCodes.Worker, PayTypes.PieceRate, null, null, hash, ct);
        _s0.Tl2 = await S0PersonAsync("tl02", "S0-TL02", "Tổ Trưởng Hai", RoleCodes.TeamLeader, PayTypes.Monthly, _s0.TCut01.TeamId, 1201, hash, ct);
        _s0.W4 = await S0PersonAsync("wk04", "S0-WK04", "Công Nhân Bốn", RoleCodes.Worker, PayTypes.Daily, _s0.TCut01.TeamId, 1004, hash, ct);
        _s0.Rs = await S0PersonAsync("rs01", "S0-RS01", "Đã Nghỉ Một", RoleCodes.Worker, PayTypes.PieceRate, null, 1009, hash, ct,
            employeeStatus: RecordStatus.Resigned, resignDate: _t.AddDays(-10));
        _s0.Tmp = await S0PersonAsync("tmp01", "S0-TMP01", "Tạm Thời Một", RoleCodes.QaQc, PayTypes.Monthly, null, null, hash, ct,
            isTemporaryPassword: true);
        _s0.Lck = await S0PersonAsync("lck01", "S0-LCK01", "Bị Khóa Một", RoleCodes.Electrician, PayTypes.Monthly, null, null, hash, ct,
            accountStatus: RecordStatus.Locked, lockedAt: _now.AddDays(-1), lockReason: "S0 seed locked account");

        // --- GradeLevel: GL1 (hiện hành), GL2 (chưa hiệu lực), GL3 ---
        _s0.Gl1 = await UpsertGradeLevelAsync("B1", new DateOnly(2025, 1, 1), 1_000_000m, ct);
        _s0.Gl2 = await UpsertGradeLevelAsync("B1", _t.AddDays(30), 1_200_000m, ct);
        _s0.Gl3 = await UpsertGradeLevelAsync("B3", new DateOnly(2025, 1, 1), 900_000m, ct);

        // --- RJ snapshots: 2001 & 2002 chưa nhập; 1001 đã nhập (= E_W1.AttendanceCode) ---
        _s0.Rj2001 = await UpsertSnapshotAsync(2001, "Nguyễn Văn An", ct);
        _s0.Rj2002 = await UpsertSnapshotAsync(2002, "Trần Thị Bình", ct);
        _s0.Rj1001 = await UpsertSnapshotAsync(1001, _s0.W1.Employee.FullName, ct);

        return _s0;
    }

    private async Task<Account> S0PersonAsync(
        string username, string employeeCode, string fullName, string roleCode, string payType,
        int? teamId, int? attendanceCode, string passwordHash, CancellationToken ct,
        string employeeStatus = RecordStatus.Active,
        string accountStatus = RecordStatus.Active,
        bool isTemporaryPassword = false,
        string? gradeCode = null,
        DateOnly? resignDate = null,
        DateTime? lockedAt = null,
        string? lockReason = null)
    {
        var employee = new Employee
        {
            EmployeeCode = employeeCode,
            FullName = fullName,
            RoleId = Role(roleCode).RoleId,
            TeamId = teamId,
            PayType = payType,
            CurrentGradeCode = gradeCode,
            AttendanceCode = attendanceCode,
            StartDate = _t.AddYears(-1),
            ResignDate = resignDate,
            Status = employeeStatus,
            CreatedAt = _now,
        };
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);

        var account = new Account
        {
            EmployeeId = employee.EmployeeId,
            Employee = employee,
            Username = username,
            PasswordHash = passwordHash,
            IsTemporaryPassword = isTemporaryPassword,
            SecurityStamp = Guid.NewGuid(),
            Status = accountStatus,
            LockedAt = lockedAt,
            LockReason = lockReason,
            CreatedAt = _now,
        };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return account;
    }

    private async Task<GradeLevelCode> UpsertGradeCodeAsync(string code, string status, CancellationToken ct)
    {
        var row = await _db.GradeLevelCodes.FirstOrDefaultAsync(g => g.GradeCode == code, ct);
        if (row is null)
        {
            row = new GradeLevelCode { GradeCode = code, Status = status, CreatedAt = _now };
            _db.GradeLevelCodes.Add(row);
        }
        else
        {
            row.Status = status;
        }

        await _db.SaveChangesAsync(ct);
        return row;
    }

    private async Task<GradeLevel> UpsertGradeLevelAsync(string code, DateOnly effective, decimal amount, CancellationToken ct)
    {
        var row = await _db.GradeLevels.FirstOrDefaultAsync(g => g.GradeCode == code && g.EffectiveDate == effective, ct);
        if (row is null)
        {
            row = new GradeLevel { GradeCode = code, EffectiveDate = effective, MonthlyAmount = amount, CreatedAt = _now };
            _db.GradeLevels.Add(row);
        }
        else
        {
            row.MonthlyAmount = amount;
        }

        await _db.SaveChangesAsync(ct);
        return row;
    }

    private async Task<RjEmployeeSnapshot> UpsertSnapshotAsync(int attendanceCode, string fullName, CancellationToken ct)
    {
        var row = await _db.RjEmployeeSnapshots.FirstOrDefaultAsync(r => r.AttendanceCode == attendanceCode, ct);
        if (row is null)
        {
            row = new RjEmployeeSnapshot
            {
                AttendanceCode = attendanceCode,
                FullName = fullName,
                RjEmployeeCode = $"RJ{attendanceCode}",
                SyncedAt = _now,
            };
            _db.RjEmployeeSnapshots.Add(row);
        }
        else
        {
            row.FullName = fullName;
            row.SyncedAt = _now;
        }

        await _db.SaveChangesAsync(ct);
        return row;
    }
}
