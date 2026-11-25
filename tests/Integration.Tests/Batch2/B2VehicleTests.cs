using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch2;

/// <summary>IT_B2_178–194 — Vehicle: A/vehicles (chỉ đọc), WK/vehicles/available, AC/vehicles (CRUD). Ca đồng thời 184 nằm ở B2ConcurrencyTests.</summary>
public sealed class B2VehicleTests(ITestOutputHelper output) : B2TestBase(output)
{
    private string Url => AC + "vehicles";

    private async Task<Vehicle?> FindAsync(string plate)
    {
        await using var db = NewDb();
        return await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(v => v.PlateNumber == plate);
    }

    [Fact]
    public async Task IT_B2_178_Create_Vehicle_Succeeds_And_Is_Audited()
    {
        var lastAudit = await LastAuditIdAsync();
        var r = await PostAsync(Url, await TkAcc(), VehicleBody("51C-222.22", "Tải 2.5 tấn", 14.50m));
        ShouldBeCreated(r);
        var row = (await FindAsync("51C-222.22"))!;
        row.VehicleType.Should().Be("Tải 2.5 tấn");
        row.FuelNormPer100Km.Should().Be(14.50m);
        row.Status.Should().Be("ACTIVE");
        await AssertAuditAddedAsync(lastAudit);
    }

    [Fact]
    public async Task IT_B2_179_Duplicate_Plate_Returns_409()
    {
        var token = await TkAcc();
        var before = await SnapshotB2Async();
        var exact = await PostAsync(Url, token, VehicleBody("51C-123.45", "Tải 1T", 10m));
        exact.Code.Should().Be(409, exact.ToString());
        AssertNoSqlLeak(exact);
        await AssertB2UnchangedAsync(before);

        var lower = await PostAsync(Url, token, VehicleBody("51c-123.45", "Tải 1T", 10m));
        AssertNoSqlLeak(lower);
        lower.Code.Should().BeOneOf(new[] { 200, 201, 409 }, lower.ToString());
        Note($"Biển số chữ thường → {lower.Code}");
    }

    [Fact]
    public async Task IT_B2_180_Plate_And_Type_Length_Boundaries()
    {
        var token = await TkAcc();
        var before = await CountAllAsync("Vehicle");
        var plate20 = "51C-" + new string('1', 16);
        var plate21 = "51C-" + new string('2', 17);
        ShouldBeCreated(await PostAsync(Url, token, VehicleBody(plate20, "Tải", 10m)));
        (await PostAsync(Url, token, VehicleBody(plate21, "Tải", 10m))).Code.Should().Be(400);
        ShouldBeCreated(await PostAsync(Url, token, VehicleBody("51C-300.01", Viet(50), 10m)));
        (await PostAsync(Url, token, VehicleBody("51C-300.02", Viet(51), 10m))).Code.Should().Be(400);

        (await FindAsync(plate20))!.PlateNumber.Length.Should().Be(20);
        (await FindAsync("51C-300.01"))!.VehicleType.Length.Should().Be(50);
        (await FindAsync(plate21)).Should().BeNull();
        (await FindAsync("51C-300.02")).Should().BeNull();
        (await CountAllAsync("Vehicle")).Should().Be(before + 2);
    }

    [Fact]
    public async Task IT_B2_181_Unicode_In_Plate_Is_Rejected_Instead_Of_Losing_Accents()
    {
        var r = await PostAsync(Url, await TkAcc(), VehicleBody("51Đ-123.45", "Tải 1T", 10m));
        r.Code.Should().Be(400, r.ToString());
        r.ShouldNotBeServerError();
        await using var db = NewDb();
        (await db.Vehicles.AsNoTracking().AnyAsync(v => v.PlateNumber.Contains("?") || v.PlateNumber.Contains("Đ"))).Should().BeFalse("không có biển số bị mất dấu");
    }

    [Fact]
    public async Task IT_B2_182_Fuel_Norm_Boundaries()
    {
        var token = await TkAcc();
        var before = await CountAllAsync("Vehicle");
        ShouldBeCreated(await PostAsync(Url, token, VehicleBody("51C-400.01", "Tải", 9999.99m)));
        var over = await PostAsync(Url, token, VehicleBody("51C-400.02", "Tải", 10000.00m));
        over.Code.Should().Be(400, over.ToString());
        AssertNoSqlLeak(over);
        (await PostAsync(Url, token, VehicleBody("51C-400.03", "Tải", 0m))).Code.Should().Be(400);
        (await PostAsync(Url, token, VehicleBody("51C-400.04", "Tải", -0.01m))).Code.Should().Be(400);
        var fraction = await PostAsync(Url, token, VehicleBody("51C-400.05", "Tải", 12.345m));
        AssertNoSqlLeak(fraction);
        fraction.Code.Should().BeOneOf(new[] { 200, 201, 400 }, fraction.ToString());
        Note($"FuelNormPer100Km=12.345 → {fraction.Code}");

        (await FindAsync("51C-400.01"))!.FuelNormPer100Km.Should().Be(9999.99m);
        foreach (var rejected in new[] { "51C-400.02", "51C-400.03", "51C-400.04" }) (await FindAsync(rejected)).Should().BeNull(rejected);
        var saved = await FindAsync("51C-400.05");
        if (fraction.Code == 400) saved.Should().BeNull();
        else saved!.FuelNormPer100Km.Should().BeOneOf(12.35m, 12.34m, 12.345m);
        (await CountAllAsync("Vehicle")).Should().Be(before + 1 + (fraction.Code == 400 ? 0 : 1));
    }

    [Fact]
    public async Task IT_B2_183_Missing_Or_Malformed_Fields_Return_400()
    {
        var token = await TkAcc();
        var before = await SnapshotB2Async();
        var bodies = new object[]
        {
            new { VehicleType = "Tải", FuelNormPer100Km = 10m },
            new { PlateNumber = "51C-500.01", FuelNormPer100Km = 10m },
            new { PlateNumber = "51C-500.02", VehicleType = "Tải", FuelNormPer100Km = (decimal?)null },
            "{\"PlateNumber\":\"51C-500.03\",\"VehicleType\":\"Tải\",\"FuelNormPer100Km\":\"abc\"}",
        };
        foreach (var body in bodies)
        {
            var r = await PostAsync(Url, token, body);
            r.Code.Should().Be(400, r.ToString());
            r.ShouldNotBeServerError();
        }

        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_185_Update_Vehicle_Then_Conflict_On_Existing_Plate()
    {
        var token = await TkAcc();
        var ok = await PutAsync(Url + "/" + B.Ve2.VehicleId, token, VehicleBody("51C-678.90", "Tải 3.5 tấn", 16m));
        ok.Code.Should().Be(200, ok.ToString());
        var conflict = await PutAsync(Url + "/" + B.Ve2.VehicleId, token, VehicleBody("51C-123.45", "Tải 9T", 99m));
        conflict.Code.Should().Be(409, conflict.ToString());
        AssertNoSqlLeak(conflict);
        var row = (await VehicleAsync(B.Ve2.VehicleId))!;
        row.PlateNumber.Should().Be("51C-678.90");
        row.VehicleType.Should().Be("Tải 3.5 tấn");
        row.FuelNormPer100Km.Should().Be(16m);
    }

    [Fact]
    public async Task IT_B2_186_Update_Missing_Vehicle_Returns_404()
    {
        var token = await TkAcc();
        var before = await SnapshotB2Async();
        (await PutAsync(Url + "/" + await MissingIdAsync(), token, VehicleBody("51C-600.01", "Tải", 10m))).Code.Should().Be(404);
        (await PutAsync(Url + "/abc", token, VehicleBody("51C-600.02", "Tải", 10m))).Code.Should().Be(404);
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_187_Discontinue_Idle_Vehicle()
    {
        var trips = await CountTripsAsync();
        var r = await PostAsync(Url + $"/{B.Ve2.VehicleId}/discontinue", await TkAcc());
        r.Code.Should().Be(200, r.ToString());
        (await VehicleAsync(B.Ve2.VehicleId))!.Status.Should().Be("DISCONTINUED");
        (await CountTripsAsync()).Should().Be(trips, "VehicleTrip không đổi");
    }

    private async Task<int> CountTripsAsync()
    {
        await using var db = NewDb();
        return await db.VehicleTrips.AsNoTracking().CountAsync();
    }

    [Fact]
    public async Task IT_B2_188_Discontinue_Vehicle_With_Open_Trip_Keeps_Trip_Intact()
    {
        var r = await PostAsync(Url + $"/{B.Ve1.VehicleId}/discontinue", await TkAcc());
        AssertNoSqlLeak(r);
        r.Code.Should().BeOneOf(new[] { 200, 400, 409 }, r.ToString());
        Note($"Ngừng xe đang có chuyến STARTED → {r.Code}");
        await using var db = NewDb();
        (await db.VehicleTrips.AsNoTracking().SingleAsync(t => t.VehicleTripId == B.Ve1Trips[0].VehicleTripId)).Status.Should().Be("STARTED");
        (await VehicleAsync(B.Ve1.VehicleId))!.Status.Should().Be(r.Code == 200 ? "DISCONTINUED" : "ACTIVE");
    }

    [Fact]
    public async Task IT_B2_189_Status_Transitions_Of_Discontinued_Vehicle()
    {
        var token = await TkAcc();
        var again = await PostAsync(Url + $"/{B.Ve3.VehicleId}/discontinue", token);
        again.Code.Should().BeOneOf(new[] { 400, 409 }, again.ToString());
        var reactivate = await PostAsync(Url + $"/{B.Ve3.VehicleId}/reactivate", token);
        reactivate.Code.Should().Be(200, reactivate.ToString());
        (await VehicleAsync(B.Ve3.VehicleId))!.Status.Should().Be("ACTIVE");
        var active = await PostAsync(Url + $"/{B.Ve2.VehicleId}/reactivate", token);
        active.Code.Should().BeOneOf(new[] { 400, 409 }, active.ToString());
        foreach (var v in new[] { B.Ve1, B.Ve2, B.Ve4 }) (await VehicleAsync(v.VehicleId))!.Status.Should().Be("ACTIVE", v.PlateNumber);
    }

    [Fact]
    public async Task IT_B2_190_Admin_Live_Status_Filters_By_Running()
    {
        var token = await TkAdm();
        var running = await GetAsync(A + "vehicles?Running=true&PageSize=100", token);
        var idle = await GetAsync(A + "vehicles?Running=false&PageSize=100", token);
        var all = await GetAsync(A + "vehicles?PageSize=100", token);
        foreach (var r in new[] { running, idle, all }) r.Code.Should().Be(200, r.ToString());

        await using var db = NewDb();
        var startedIds = await db.VehicleTrips.AsNoTracking().Where(t => t.Status == "STARTED").Select(t => t.VehicleId).Distinct().ToListAsync();
        var allIds = await db.Vehicles.AsNoTracking().Select(v => v.VehicleId).ToListAsync();
        var runningIds = Ids(running, "vehicleId");
        runningIds.Should().Contain(new[] { B.Ve1.VehicleId, B.Ve4.VehicleId }).And.NotContain(new[] { B.Ve2.VehicleId, B.Ve3.VehicleId });
        runningIds.Should().BeEquivalentTo(startedIds, "IsRunning khớp VehicleTrip.Status='STARTED'");
        foreach (var item in running.Items())
        {
            B1Json.Bool(item, "isRunning").Should().BeTrue();
            B1Json.Int(item, "currentTripId").Should().NotBeNull();
            B1Json.Str(item, "driverName").Should().NotBeNullOrEmpty();
        }

        var idleIds = Ids(idle, "vehicleId");
        idleIds.Should().Contain(new[] { B.Ve2.VehicleId, B.Ve3.VehicleId }).And.NotContain(new[] { B.Ve1.VehicleId, B.Ve4.VehicleId });
        idleIds.Should().BeEquivalentTo(allIds.Except(startedIds));
        Ids(all, "vehicleId").Should().BeEquivalentTo(allIds);
    }

    [Fact]
    public async Task IT_B2_191_Vehicle_With_Two_Open_Trips_Is_Listed_Once_And_Flagged()
    {
        var second = await SeedVe4SecondOpenTripAsync();
        var r = await GetAsync(A + "vehicles?Running=true&PageSize=100", await TkAdm());
        r.Code.Should().Be(200, r.ToString());
        r.ShouldNotBeServerError();
        var rows = r.Items().Where(i => B1Json.Int(i, "vehicleId") == B.Ve4.VehicleId).ToList();
        rows.Should().ContainSingle("VE4 không bị nhân đôi trong danh sách");
        B1Json.Int(rows[0], "openTripCount").Should().Be(2, "đánh dấu dữ liệu bất thường");
        var current = B1Json.Int(rows[0], "currentTripId");
        Note($"CurrentTripId của VE4 = {current} (các chuyến: {B.Ve4Trips[0].VehicleTripId}, {second.VehicleTripId})");
        current.Should().BeOneOf(B.Ve4Trips[0].VehicleTripId, second.VehicleTripId);
    }

    [Fact]
    public async Task IT_B2_192_Invalid_Running_Filter_Returns_400()
    {
        var before = await SnapshotB2Async();
        var r = await GetAsync(A + "vehicles?Running=abc", await TkAdm());
        r.Code.Should().Be(400, r.ToString());
        r.ShouldNotBeServerError();
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_193_Warehouse_Keeper_Sees_Only_Available_Vehicles()
    {
        var before = await SnapshotB2Async();
        var r = await GetAsync(WK + "vehicles/available", await TkWk());
        r.Code.Should().Be(200, r.ToString());
        var ids = Ids(r, "vehicleId");
        ids.Should().Contain(B.Ve2.VehicleId).And.NotContain(new[] { B.Ve1.VehicleId, B.Ve3.VehicleId, B.Ve4.VehicleId });
        await using var db = NewDb();
        var started = await db.VehicleTrips.AsNoTracking().Where(t => t.Status == "STARTED").Select(t => t.VehicleId).ToListAsync();
        ids.Should().BeEquivalentTo(await db.Vehicles.AsNoTracking().Where(v => v.Status == "ACTIVE" && !started.Contains(v.VehicleId))
            .Select(v => v.VehicleId).ToListAsync(), "chỉ xe ACTIVE và không có chuyến STARTED");
        await AssertB2UnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B2_194_Accountant_List_Reports_Trip_Count_And_Last_Odometer()
    {
        var r = await GetAsync(Url + "?Status=Active&PageSize=100", await TkAcc());
        r.Code.Should().Be(200, r.ToString());
        r.Items().Should().NotBeEmpty();
        await using var db = NewDb();
        foreach (var item in r.Items())
        {
            var id = B1Json.Int(item, "vehicleId")!.Value;
            B1Json.Str(item, "status")!.ToUpperInvariant().Should().Be("ACTIVE");
            var trips = await db.VehicleTrips.AsNoTracking().Where(t => t.VehicleId == id).ToListAsync();
            B1Json.Int(item, "tripCount").Should().Be(trips.Count, $"TripCount khớp COUNT(VehicleTrip) của xe {id}");
            var last = B1Json.Int(item, "lastOdometer");
            var allowed = new List<int?> { null };
            allowed.AddRange(trips.Where(t => t.OdometerEnd != null).Select(t => t.OdometerEnd));
            allowed.AddRange(trips.Select(t => (int?)t.OdometerStart));
            allowed.Should().Contain(last, $"LastOdometer của xe {id} khớp một số km trong VehicleTrip");
        }
    }
}
