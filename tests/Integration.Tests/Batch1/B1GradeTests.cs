using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>IT_B1_186–207 — UC15 (chỉ Admin): GradeCodes và GradeLevels.</summary>
public sealed class B1GradeTests(ITestOutputHelper output) : B1TestBase(output)
{
    private string Codes => A + "grade-codes";
    private string Levels => A + "grade-levels";

    private async Task<GradeLevel?> LevelAsync(int id)
    {
        await using var db = NewDb();
        return await db.GradeLevels.AsNoTracking().SingleOrDefaultAsync(g => g.GradeLevelId == id);
    }

    private async Task<GradeLevel> AddLevelAsync(string code, DateOnly date, decimal amount)
    {
        var level = new GradeLevel { GradeCode = code, EffectiveDate = date, MonthlyAmount = amount, CreatedAt = Clock.Now };
        Db.GradeLevels.Add(level);
        await Db.SaveChangesAsync();
        return level;
    }

    private async Task<bool> CodeExistsAsync(string code)
    {
        await using var db = NewDb();
        return await db.GradeLevelCodes.AsNoTracking().AnyAsync(g => g.GradeCode == code);
    }

    private async Task<int> LevelCountAsync(string code)
    {
        await using var db = NewDb();
        return await db.GradeLevels.AsNoTracking().CountAsync(g => g.GradeCode == code);
    }

    // ---------------------------------------------------------------- 186–192 GradeCodes

    [Fact]
    public async Task IT_B1_186_List_Grade_Codes_Includes_Inactive_And_InUse_Matches_Db()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await GetAsync(Codes, token);

        r.Code.Should().Be(200, r.ToString());
        var byCode = r.Items().ToDictionary(i => B1Json.Str(i, "gradeCode")!, i => B1Json.Bool(i, "inUse"));
        byCode.Keys.Should().Contain(["B1", "B2", "B3", "B9"]);
        byCode["B1"].Should().BeTrue();
        byCode["B3"].Should().BeTrue();
        byCode["B2"].Should().BeFalse();

        await using var db = NewDb();
        foreach (var (code, inUse) in byCode)
        {
            var expected = await db.Employees.AsNoTracking().AnyAsync(e => e.CurrentGradeCode == code)
                           || await db.GradeLevels.AsNoTracking().AnyAsync(g => g.GradeCode == code)
                           || await db.GradeEvaluations.AsNoTracking().AnyAsync(g => g.CurrentGradeCode == code || g.ProposedGradeCode == code);
            inUse.Should().Be(expected, $"InUse của {code}");
        }

        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_187_Create_Grade_Code_Active_With_CreatedAt()
    {
        var token = await TokenAsync(S0.Adm);

        var r = await PostAsync(Codes, token, new { GradeCode = "B4" });

        ShouldBeCreated(r);
        await using var db = NewDb();
        var row = await db.GradeLevelCodes.AsNoTracking().SingleAsync(g => g.GradeCode == "B4");
        row.Status.Should().Be(GradeCodes.Active);
        row.CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task IT_B1_188_Create_Grade_Code_Differing_Only_By_Case_Returns_409()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(Codes, token, new { GradeCode = "b1" });

        r.ShouldNotBeServerError();
        r.Code.Should().Be(409, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_189_Grade_Code_Length_Boundaries()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var six = await PostAsync(Codes, token, new { GradeCode = "B12345" });
        var empty = await PostAsync(Codes, token, new { GradeCode = "" });
        var blank = await PostAsync(Codes, token, new { GradeCode = "  " });

        six.Code.Should().Be(400, six.ToString());
        empty.Code.Should().Be(400, empty.ToString());
        blank.Code.Should().Be(400, blank.ToString());
        await AssertDbUnchangedAsync(before);

        var five = await PostAsync(Codes, token, new { GradeCode = "B1234" });

        ShouldBeCreated(five);
        (await CodeExistsAsync("B1234")).Should().BeTrue();
        (await CodeExistsAsync("B12345")).Should().BeFalse();
    }

    [Fact]
    public async Task IT_B1_190_Delete_Unused_Grade_Code()
    {
        var token = await TokenAsync(S0.Adm);

        var r = await DeleteAsync(Codes + "/B2", token);

        r.Code.Should().BeOneOf(200, 204);
        (await CodeExistsAsync("B2")).Should().BeFalse();
    }

    [Fact]
    public async Task IT_B1_191_Delete_Grade_Code_In_Use_Returns_409_Not_FK_Error()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var b1 = await DeleteAsync(Codes + "/B1", token);
        var b3 = await DeleteAsync(Codes + "/B3", token);

        b1.Code.Should().Be(409, b1.ToString());
        b3.Code.Should().Be(409, b3.ToString());
        (await CodeExistsAsync("B1")).Should().BeTrue();
        (await CodeExistsAsync("B3")).Should().BeTrue();
        (await LevelAsync(S0.Gl1.GradeLevelId)).Should().NotBeNull();
        (await LevelAsync(S0.Gl3.GradeLevelId)).Should().NotBeNull();
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_192_Delete_Unknown_Grade_Code_Returns_404()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await DeleteAsync(Codes + "/ZZ", token);

        r.Code.Should().Be(404, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 193–200 GradeLevels (đọc / tạo)

    [Fact]
    public async Task IT_B1_193_List_Grade_Levels_IsCurrent_Matches_Latest_Effective_Date()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await GetAsync(Levels, token);

        r.Code.Should().Be(200, r.ToString());
        var byId = r.Items().ToDictionary(i => B1Json.Int(i, "gradeLevelId")!.Value, i => B1Json.Bool(i, "isCurrent")!.Value);
        byId[S0.Gl1.GradeLevelId].Should().BeTrue();
        byId[S0.Gl2.GradeLevelId].Should().BeFalse();
        byId[S0.Gl3.GradeLevelId].Should().BeTrue();

        await using var db = NewDb();
        var levels = await db.GradeLevels.AsNoTracking().ToListAsync();
        foreach (var l in levels.Where(l => byId.ContainsKey(l.GradeLevelId)))
        {
            var latest = levels.Where(x => x.GradeCode == l.GradeCode && x.EffectiveDate <= Clock.Today).Select(x => (DateOnly?)x.EffectiveDate).Max();
            byId[l.GradeLevelId].Should().Be(l.EffectiveDate <= Clock.Today && l.EffectiveDate == latest, $"IsCurrent của {l.GradeCode} {l.EffectiveDate}");
        }

        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_194_Create_Grade_Level_Stores_Amount_And_Date()
    {
        var token = await TokenAsync(S0.Adm);
        var date = Clock.Today.AddDays(10);

        var r = await PostAsync(Levels, token, new { GradeCode = "B2", MonthlyAmount = 5_000_000m, EffectiveDate = date });

        ShouldBeCreated(r);
        await using var db = NewDb();
        var row = await db.GradeLevels.AsNoTracking().SingleAsync(g => g.GradeCode == "B2" && g.EffectiveDate == date);
        row.MonthlyAmount.Should().Be(5_000_000.00m);
    }

    [Fact]
    public async Task IT_B1_195_Create_Grade_Level_Duplicate_Code_And_Date_Returns_409()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(Levels, token, new { GradeCode = "B1", MonthlyAmount = 6_000_000m, EffectiveDate = S0.Gl2.EffectiveDate });

        r.ShouldNotBeServerError();
        r.Code.Should().Be(409, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_196_Create_Grade_Level_For_Unknown_Or_Inactive_Code_Is_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        var date = Clock.Today.AddDays(12);
        var before = await SnapshotAsync();

        var unknown = await PostAsync(Levels, token, new { GradeCode = "ZZ", MonthlyAmount = 1000m, EffectiveDate = date });
        var inactive = await PostAsync(Levels, token, new { GradeCode = "B9", MonthlyAmount = 1000m, EffectiveDate = date });

        unknown.ShouldNotBeServerError();
        unknown.Code.Should().BeOneOf(400, 404);
        inactive.Code.Should().Be(400, inactive.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_197_Monthly_Amount_Lower_Boundaries()
    {
        var token = await TokenAsync(S0.Adm);
        using var scope = new AssertionScope();

        var negative = await PostAsync(Levels, token, new { GradeCode = "B2", MonthlyAmount = -1m, EffectiveDate = Clock.Today.AddDays(20) });
        var zero = await PostAsync(Levels, token, new { GradeCode = "B2", MonthlyAmount = 0m, EffectiveDate = Clock.Today.AddDays(21) });
        var cent = await PostAsync(Levels, token, new { GradeCode = "B2", MonthlyAmount = 0.01m, EffectiveDate = Clock.Today.AddDays(22) });

        negative.Code.Should().Be(400, negative.ToString());
        zero.Code.Should().Be(400, zero.ToString());
        ShouldBeCreated(cent);
        await using var db = NewDb();
        var saved = await db.GradeLevels.AsNoTracking().Where(g => g.GradeCode == "B2").Select(g => g.MonthlyAmount).ToListAsync();
        saved.Should().BeEquivalentTo(new[] { 0.01m }, "chỉ ca hợp lệ (0.01) được lưu");
    }

    [Fact]
    public async Task IT_B1_198_Monthly_Amount_Upper_Boundary_decimal_18_2()
    {
        var token = await TokenAsync(S0.Adm);
        using var scope = new AssertionScope();

        var max = await PostAsync(Levels, token, new { GradeCode = "B2", MonthlyAmount = 9999999999999999.99m, EffectiveDate = Clock.Today.AddDays(23) });
        var over = await PostAsync(Levels, token, """{"GradeCode":"B2","MonthlyAmount":10000000000000000.00,"EffectiveDate":"2026-12-18"}""");

        ShouldBeCreated(max);
        over.ShouldNotBeServerError();
        over.Code.Should().Be(400, over.ToString());
        await using var db = NewDb();
        (await db.GradeLevels.AsNoTracking().Where(g => g.GradeCode == "B2" && g.EffectiveDate == Clock.Today.AddDays(23))
            .Select(g => g.MonthlyAmount).SingleAsync()).Should().Be(9999999999999999.99m);
        (await LevelCountAsync("B2")).Should().Be(1);
    }

    [Fact]
    public async Task IT_B1_199_Monthly_Amount_With_Three_Decimals_Rounded_Or_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        var date = Clock.Today.AddDays(25);

        var r = await PostAsync(Levels, token, new { GradeCode = "B2", MonthlyAmount = 1000.555m, EffectiveDate = date });

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 201, 400);
        if (r.Code is 200 or 201)
        {
            await using var db = NewDb();
            var saved = await db.GradeLevels.AsNoTracking().Where(g => g.GradeCode == "B2" && g.EffectiveDate == date).Select(g => g.MonthlyAmount).SingleAsync();
            saved.Should().BeOneOf(1000.56m, 1000.55m);
            Note($"IT_B1_199 làm tròn 1000.555 => {saved}");
        }
        else
        {
            (await LevelCountAsync("B2")).Should().Be(0);
        }
    }

    [Fact]
    public async Task IT_B1_200_Create_Grade_Level_With_Past_Effective_Date_Is_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PostAsync(Levels, token, new { GradeCode = "B2", MonthlyAmount = 5_000_000m, EffectiveDate = PastDay(Clock.Today) });

        r.ShouldNotBeServerError();
        r.Code.Should().Be(400, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 201–207 GradeLevels (sửa / xóa)

    [Fact]
    public async Task IT_B1_201_Update_Future_Grade_Level()
    {
        var token = await TokenAsync(S0.Adm);
        var gl1Before = await LevelAsync(S0.Gl1.GradeLevelId);
        var newDate = Clock.Today.AddDays(45);

        var r = await PutAsync(Levels + $"/{S0.Gl2.GradeLevelId}", token, new { MonthlyAmount = 6_500_000m, EffectiveDate = newDate });

        r.Code.Should().Be(200, r.ToString());
        var gl2 = await LevelAsync(S0.Gl2.GradeLevelId);
        gl2!.MonthlyAmount.Should().Be(6_500_000.00m);
        gl2.EffectiveDate.Should().Be(newDate);
        var gl1After = await LevelAsync(S0.Gl1.GradeLevelId);
        (gl1After!.MonthlyAmount, gl1After.EffectiveDate).Should().Be((gl1Before!.MonthlyAmount, gl1Before.EffectiveDate));
    }

    [Fact]
    public async Task IT_B1_202_Update_Effective_Grade_Level_Is_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PutAsync(Levels + $"/{S0.Gl1.GradeLevelId}", token, new { MonthlyAmount = 9_999_999m, EffectiveDate = new DateOnly(2025, 1, 1) });

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(400, 409);
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_203_Grade_Level_Effective_Today_Boundary_For_Update_And_Delete()
    {
        var gl4 = await AddLevelAsync("B2", Clock.Today, 4_000_000m);
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var put = await PutAsync(Levels + $"/{gl4.GradeLevelId}", token, new { MonthlyAmount = 4_500_000m, EffectiveDate = Clock.Today });
        put.ShouldNotBeServerError();
        put.Code.Should().BeOneOf(200, 400, 409);
        Note($"IT_B1_203 PUT mức hiệu lực hôm nay => {put.Code}");
        if (put.Code != 200) await AssertDbUnchangedAsync(before);

        var afterPut = await SnapshotAsync();
        var del = await DeleteAsync(Levels + $"/{gl4.GradeLevelId}", token);
        del.ShouldNotBeServerError();
        del.Code.Should().BeOneOf(200, 204, 400, 409);
        Note($"IT_B1_203 DELETE mức hiệu lực hôm nay => {del.Code}");
        if (del.Code is 400 or 409) await AssertDbUnchangedAsync(afterPut);
        else (await LevelAsync(gl4.GradeLevelId)).Should().BeNull();
    }

    [Fact]
    public async Task IT_B1_204_Update_Grade_Level_To_Date_Of_Another_Level_Returns_409()
    {
        await AddLevelAsync("B1", Clock.Today.AddDays(60), 7_000_000m);
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PutAsync(Levels + $"/{S0.Gl2.GradeLevelId}", token, new { MonthlyAmount = 6_000_000m, EffectiveDate = Clock.Today.AddDays(60) });

        r.ShouldNotBeServerError();
        r.Code.Should().Be(409, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_205_Update_Future_Grade_Level_To_Past_Date_Returns_400()
    {
        var token = await TokenAsync(S0.Adm);
        var before = await SnapshotAsync();

        var r = await PutAsync(Levels + $"/{S0.Gl2.GradeLevelId}", token, new { MonthlyAmount = 6_000_000m, EffectiveDate = PastDay(Clock.Today) });

        r.Code.Should().Be(400, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_206_Delete_Future_Grade_Level_Keeps_Current_One()
    {
        var token = await TokenAsync(S0.Adm);

        var r = await DeleteAsync(Levels + $"/{S0.Gl2.GradeLevelId}", token);

        r.Code.Should().BeOneOf(200, 204);
        (await LevelAsync(S0.Gl2.GradeLevelId)).Should().BeNull();
        (await LevelAsync(S0.Gl1.GradeLevelId)).Should().NotBeNull();
        var list = await GetAsync(Levels, token);
        list.Items().Single(i => B1Json.Int(i, "gradeLevelId") == S0.Gl1.GradeLevelId).Let(i => B1Json.Bool(i, "isCurrent")).Should().BeTrue();
    }

    [Fact]
    public async Task IT_B1_207_Delete_Effective_Or_Unknown_Grade_Level_Is_Rejected()
    {
        var token = await TokenAsync(S0.Adm);
        await using var db = NewDb();
        var missing = (await db.GradeLevels.AsNoTracking().MaxAsync(g => g.GradeLevelId)) + 1_000_000;
        var before = await SnapshotAsync();

        var effective = await DeleteAsync(Levels + $"/{S0.Gl1.GradeLevelId}", token);
        var unknown = await DeleteAsync(Levels + $"/{missing}", token);

        effective.Code.Should().BeOneOf(400, 409);
        unknown.Code.Should().Be(404, unknown.ToString());
        (await LevelAsync(S0.Gl1.GradeLevelId)).Should().NotBeNull();
        await AssertDbUnchangedAsync(before);
    }
}

internal static class FluentLetExtensions
{
    public static TOut Let<TIn, TOut>(this TIn value, Func<TIn, TOut> f) => f(value);
}
