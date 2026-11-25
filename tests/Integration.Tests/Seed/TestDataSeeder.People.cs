using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

public sealed partial class TestDataSeeder
{
    private async Task SeedPeopleAsync(CancellationToken ct)
    {
        // Văn phòng — MONTHLY, gắn TEAM-OFFICE (trừ accountant không gắn tổ may — dùng OFFICE)
        _ctx.Admin01 = await CreatePersonAsync(
            "admin_01", "EMP-IT-ADM01", "Admin One", RoleCodes.Admin,
            PayTypes.Monthly, _ctx.TeamOffice.TeamId, RecordStatus.Active, RecordStatus.Active, ct);
        _ctx.Admin02 = await CreatePersonAsync(
            "admin_02", "EMP-IT-ADM02", "Admin Two", RoleCodes.Admin,
            PayTypes.Monthly, _ctx.TeamOffice.TeamId, RecordStatus.Active, RecordStatus.Active, ct);
        _ctx.Manager01 = await CreatePersonAsync(
            "manager_01", "EMP-IT-MGR01", "Manager One", RoleCodes.Manager,
            PayTypes.Monthly, _ctx.TeamOffice.TeamId, RecordStatus.Active, RecordStatus.Active, ct);
        _ctx.Accountant01 = await CreatePersonAsync(
            "accountant_01", "EMP-IT-ACC01", "Accountant One", RoleCodes.Accountant,
            PayTypes.Monthly, teamId: null, RecordStatus.Active, RecordStatus.Active, ct);
        _ctx.Warehouse01 = await CreatePersonAsync(
            "warehouse_01", "EMP-IT-WH01", "Warehouse One", RoleCodes.WarehouseKeeper,
            PayTypes.Monthly, _ctx.TeamOffice.TeamId, RecordStatus.Active, RecordStatus.Active, ct);
        _ctx.Qaqc01 = await CreatePersonAsync(
            "qaqc_01", "EMP-IT-QC01", "QAQC One", RoleCodes.QaQc,
            PayTypes.Monthly, _ctx.TeamOffice.TeamId, RecordStatus.Active, RecordStatus.Active, ct);
        _ctx.Electrician01 = await CreatePersonAsync(
            "electrician_01", "EMP-IT-EL01", "Electrician One", RoleCodes.Electrician,
            PayTypes.Monthly, _ctx.TeamOffice.TeamId, RecordStatus.Active, RecordStatus.Active, ct);

        // Tổ may A
        _ctx.LeaderSewA = await CreatePersonAsync(
            "leader_sew_a", "EMP-IT-LSA", "Leader Sew A", RoleCodes.TeamLeader,
            PayTypes.Monthly, _ctx.TeamSewA.TeamId, RecordStatus.Active, RecordStatus.Active, ct);
        _ctx.DeputySewA = await CreatePersonAsync(
            "deputy_sew_a", "EMP-IT-DSA", "Deputy Sew A", RoleCodes.TeamDeputy,
            PayTypes.Monthly, _ctx.TeamSewA.TeamId, RecordStatus.Active, RecordStatus.Active, ct);
        _ctx.WorkerSewA1 = await CreatePersonAsync(
            "worker_sew_a1", "EMP-IT-WSA1", "Worker Sew A1", RoleCodes.Worker,
            PayTypes.PieceRate, _ctx.TeamSewA.TeamId, RecordStatus.Active, RecordStatus.Active, ct, attendanceCode: 101);
        _ctx.WorkerSewA2 = await CreatePersonAsync(
            "worker_sew_a2", "EMP-IT-WSA2", "Worker Sew A2", RoleCodes.Worker,
            PayTypes.PieceRate, _ctx.TeamSewA.TeamId, RecordStatus.Active, RecordStatus.Active, ct, attendanceCode: 102);

        // Tổ may B
        _ctx.LeaderSewB = await CreatePersonAsync(
            "leader_sew_b", "EMP-IT-LSB", "Leader Sew B", RoleCodes.TeamLeader,
            PayTypes.Monthly, _ctx.TeamSewB.TeamId, RecordStatus.Active, RecordStatus.Active, ct);
        _ctx.WorkerSewB1 = await CreatePersonAsync(
            "worker_sew_b1", "EMP-IT-WSB1", "Worker Sew B1", RoleCodes.Worker,
            PayTypes.PieceRate, _ctx.TeamSewB.TeamId, RecordStatus.Active, RecordStatus.Active, ct, attendanceCode: 201);

        // Tổ cắt
        _ctx.WorkerCut01 = await CreatePersonAsync(
            "worker_cut_01", "EMP-IT-WC01", "Worker Cut 01", RoleCodes.Worker,
            PayTypes.Daily, _ctx.TeamCut01.TeamId, RecordStatus.Active, RecordStatus.Active, ct, attendanceCode: 301);

        // LOCKED / RESIGNED
        _ctx.WorkerLocked = await CreatePersonAsync(
            "worker_locked", "EMP-IT-WLOCK", "Worker Locked", RoleCodes.Worker,
            PayTypes.PieceRate, _ctx.TeamSewA.TeamId, RecordStatus.Active, RecordStatus.Locked, ct,
            lockedAt: _now, lockReason: "IT seed locked account");
        _ctx.WorkerResigned = await CreatePersonAsync(
            "worker_resigned", "EMP-IT-WRES", "Worker Resigned", RoleCodes.Worker,
            PayTypes.PieceRate, _ctx.TeamSewA.TeamId, RecordStatus.Resigned, RecordStatus.Locked, ct,
            resignDate: _t.AddDays(-30),
            lockedAt: _now.AddDays(-30),
            lockReason: "IT seed resigned employee");
    }

    private async Task<Account> CreatePersonAsync(
        string username,
        string employeeCode,
        string fullName,
        string roleCode,
        string payType,
        int? teamId,
        string employeeStatus,
        string accountStatus,
        CancellationToken ct,
        int? attendanceCode = null,
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
            Username = username,
            PasswordHash = _passwordHash,
            IsTemporaryPassword = false,
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
}
