using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Seed;

/// <summary>
/// Seed Minimum Test Account + Minimum Domain Data theo Report-5.0.
/// Tham chiếu bằng mã nghiệp vụ; ID lấy sau SaveChanges vào <see cref="TestContext"/>.
/// </summary>
public sealed partial class TestDataSeeder
{
    private readonly NxMtdDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly FixedClock _clock;
    private readonly TestContext _ctx = new();
    private readonly DateOnly _t;
    private readonly DateTime _now;
    private readonly string _passwordHash;

    public TestDataSeeder(NxMtdDbContext db, IPasswordHasher hasher, FixedClock clock)
    {
        _db = db;
        _hasher = hasher;
        _clock = clock;
        _t = clock.Today;
        _now = clock.Now;
        _passwordHash = hasher.Hash(TestContext.TestPassword);
    }

    public async Task<TestContext> SeedAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync(ct);
        await SeedTeamsAsync(ct);
        await SeedPeopleAsync(ct);
        await SeedCatalogAsync(ct);
        await SeedOrdersAsync(ct);
        await SeedOperationsAndRatesAsync(ct);
        await SeedMarkersAsync(ct);
        await SeedWarehouseAsync(ct);
        await SeedProductionFloorAsync(ct);
        await SeedFinanceAsync(ct);
        await SeedAttendanceAsync(ct);
        await SeedRequestsAsync(ct);
        await SeedPurchaseAndVehicleAsync(ct);
        await SeedSettingsAsync(ct);
        return _ctx;
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        var defs = new (string Code, string Name, bool Prod)[]
        {
            (RoleCodes.Admin, "Giám đốc (Admin)", false),
            (RoleCodes.Manager, "Quản đốc", false),
            (RoleCodes.Accountant, "Kế toán", false),
            (RoleCodes.WarehouseKeeper, "Thủ kho", false),
            (RoleCodes.TeamLeader, "Tổ trưởng", true),
            (RoleCodes.TeamDeputy, "Tổ phó", true),
            (RoleCodes.QaQc, "QA/QC", true),
            (RoleCodes.Electrician, "Cơ điện", true),
            (RoleCodes.Worker, "Công nhân", true),
        };

        foreach (var (code, name, prod) in defs)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleCode == code, ct);
            if (role is null)
            {
                role = new Role { RoleCode = code, RoleName = name, IsProduction = prod };
                _db.Roles.Add(role);
                await _db.SaveChangesAsync(ct);
            }

            _ctx.Roles[code] = role;
        }
    }

    private async Task SeedTeamsAsync(CancellationToken ct)
    {
        _ctx.TeamOffice = await UpsertTeamAsync("TEAM-OFFICE", TeamTypes.Office, RecordStatus.Active, ct);
        _ctx.TeamCut01 = await UpsertTeamAsync("TEAM-CUT-01", TeamTypes.Cutting, RecordStatus.Active, ct);
        _ctx.TeamSewA = await UpsertTeamAsync("TEAM-SEW-A", TeamTypes.Sewing, RecordStatus.Active, ct);
        _ctx.TeamSewB = await UpsertTeamAsync("TEAM-SEW-B", TeamTypes.Sewing, RecordStatus.Active, ct);
        _ctx.TeamSewX = await UpsertTeamAsync("TEAM-SEW-X", TeamTypes.Sewing, RecordStatus.Discontinued, ct);
    }

    private async Task<Team> UpsertTeamAsync(string code, string type, string status, CancellationToken ct)
    {
        var team = await _db.Teams.FirstOrDefaultAsync(t => t.TeamCode == code, ct);
        if (team is null)
        {
            team = new Team
            {
                TeamCode = code,
                TeamType = type,
                Status = status,
                CreatedAt = _now,
            };
            _db.Teams.Add(team);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            team.TeamType = type;
            team.Status = status;
            await _db.SaveChangesAsync(ct);
        }

        return team;
    }

    private Role Role(string code) => _ctx.Roles[code];
}
