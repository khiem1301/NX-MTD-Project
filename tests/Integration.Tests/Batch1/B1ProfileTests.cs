using FluentAssertions;
using Xunit.Abstractions;

namespace Integration.Tests.Batch1;

/// <summary>IT_B1_024–038 — C/Profile (xem, tự sửa DOB/Phone, đổi mật khẩu).</summary>
public sealed class B1ProfileTests(ITestOutputHelper output) : B1TestBase(output)
{
    private static string Pw => Seed.S0Context.Password;
    private const string NewPw = "N3w@Pass!";

    /// <summary>[?] Chính sách độ dài tối thiểu chưa được xác nhận trong code đã đọc; giả định min = 8, cần Business xác nhận.</summary>
    private const int AssumedMinPasswordLength = 8;

    private async Task AssertSingleAuditForAsync(long lastId, long recordId)
    {
        var rows = await AuditRowsAfterAsync(lastId);
        rows.Should().HaveCount(1, "AuditLogs +1");
        rows[0].RecordId.Should().Be(recordId);
        Note($"AuditLog: Table={rows[0].TableName}, Action={rows[0].Action}, ChangedBy={rows[0].ChangedBy}");
    }

    // ---------------------------------------------------------------- 024–031

    [Fact]
    public async Task IT_B1_024_Get_Profile_Returns_Own_Data_Without_Sensitive_Fields()
    {
        var token = await TokenAsync(S0.W1);
        var before = await SnapshotAsync();

        var r = await GetAsync(C + "Profile", token);

        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "employeeId").Should().Be(S0.W1.EmployeeId);
        B1Json.Str(r.Data, "username").Should().Be("wk01");
        B1Json.Prop(r.Data, "isTemporaryPassword").Should().NotBeNull();
        foreach (var hidden in new[] { "payType", "attendanceCode", "status", "accountStatus" })
            B1Json.Prop(r.Data, hidden).Should().BeNull($"UC04: không lộ '{hidden}'");
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_025_Put_Profile_Updates_Dob_And_Phone_Only()
    {
        var token = await TokenAsync(S0.W1);
        var before = await EmployeeAsync(S0.W1.EmployeeId);
        var lastAudit = await LastAuditIdAsync();

        var r = await PutAsync(C + "Profile", token, new { DateOfBirth = new DateOnly(1995, 5, 20), Phone = "0901234567" });

        r.Code.Should().Be(200, r.ToString());
        var after = await EmployeeAsync(S0.W1.EmployeeId);
        after.DateOfBirth.Should().Be(new DateOnly(1995, 5, 20));
        after.Phone.Should().Be("0901234567");
        after.UpdatedAt.Should().NotBeNull();
        after.RowVer.Should().NotEqual(before.RowVer);
        after.FullName.Should().Be(before.FullName);
        after.RoleId.Should().Be(before.RoleId);
        after.TeamId.Should().Be(before.TeamId);
        after.PayType.Should().Be(before.PayType);
        after.Status.Should().Be(before.Status);
        after.AttendanceCode.Should().Be(before.AttendanceCode);
        after.CurrentGradeCode.Should().Be(before.CurrentGradeCode);
        after.StartDate.Should().Be(before.StartDate);
        await AssertSingleAuditForAsync(lastAudit, S0.W1.EmployeeId);
    }

    [Fact]
    public async Task IT_B1_026_Put_Profile_Empty_Values_Clear_Dob_And_Phone()
    {
        S0.W1.Employee.Phone = "0909998887";
        S0.W1.Employee.DateOfBirth = new DateOnly(1990, 1, 1);
        await Db.SaveChangesAsync();
        var token = await TokenAsync(S0.W1);

        var r = await PutAsync(C + "Profile", token, new { DateOfBirth = (DateOnly?)null, Phone = "" });

        r.Code.Should().Be(200, r.ToString());
        var after = await EmployeeAsync(S0.W1.EmployeeId);
        after.Phone.Should().BeNull();
        after.DateOfBirth.Should().BeNull();
    }

    [Fact]
    public async Task IT_B1_027_Put_Profile_Phone_Of_Exactly_15_Characters_Is_Stored_In_Full()
    {
        var token = await TokenAsync(S0.W1);
        const string phone15 = "012345678901234";

        var r = await PutAsync(C + "Profile", token, new { DateOfBirth = (DateOnly?)null, Phone = phone15 });

        r.Code.Should().Be(200, r.ToString());
        var after = await EmployeeAsync(S0.W1.EmployeeId);
        after.Phone.Should().Be(phone15);
        after.Phone!.Length.Should().Be(15);
    }

    [Fact]
    public async Task IT_B1_028_Put_Profile_Phone_Of_16_Characters_Returns_400_Not_500()
    {
        var token = await TokenAsync(S0.W1);
        var before = await SnapshotAsync();

        var r = await PutAsync(C + "Profile", token, new { DateOfBirth = (DateOnly?)null, Phone = "0123456789012345" });

        r.Code.Should().Be(400, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_029_Put_Profile_Dob_Tomorrow_Rejected_And_Dob_1900_Not_500()
    {
        var token = await TokenAsync(S0.W1);
        var before = await SnapshotAsync();

        var tomorrow = await PutAsync(C + "Profile", token, new { DateOfBirth = FutureDay(Clock.Today), Phone = "0901234567" });

        tomorrow.Code.Should().Be(400, tomorrow.ToString());
        await AssertDbUnchangedAsync(before);

        var old = await PutAsync(C + "Profile", token, new { DateOfBirth = new DateOnly(1900, 1, 1), Phone = "0901234567" });

        old.Code.Should().BeOneOf(200, 400);
        Note($"IT_B1_029 DOB 1900-01-01 => {old.Code}");
        if (old.Code == 400) await AssertDbUnchangedAsync(before);
        else (await EmployeeAsync(S0.W1.EmployeeId)).DateOfBirth.Should().Be(new DateOnly(1900, 1, 1));
    }

    [Fact]
    public async Task IT_B1_030_Put_Profile_Mass_Assignment_And_Idor_Only_Change_Own_Dob_Phone()
    {
        var token = await TokenAsync(S0.W1);
        var w1Before = await EmployeeAsync(S0.W1.EmployeeId);
        var admBefore = await EmployeeAsync(S0.Adm.EmployeeId);
        var admAccountBefore = await AccountAsync(S0.Adm.AccountId);
        var payload = $$"""
            {"DateOfBirth":"1992-03-04","Phone":"0987654321","EmployeeId":{{S0.Adm.EmployeeId}},
             "FullName":"Hack","PayType":"Monthly","Status":"RESIGNED","RoleId":{{admBefore.RoleId}}}
            """;

        var r = await PutAsync(C + "Profile", token, payload);

        r.Code.Should().Be(200, r.ToString());
        var w1After = await EmployeeAsync(S0.W1.EmployeeId);
        w1After.DateOfBirth.Should().Be(new DateOnly(1992, 3, 4));
        w1After.Phone.Should().Be("0987654321");
        w1After.FullName.Should().Be(w1Before.FullName);
        w1After.PayType.Should().Be(w1Before.PayType);
        w1After.Status.Should().Be(w1Before.Status);
        w1After.RoleId.Should().Be(w1Before.RoleId);

        var admAfter = await EmployeeAsync(S0.Adm.EmployeeId);
        admAfter.Should().BeEquivalentTo(admBefore, o => o.Excluding(e => e.Role).Excluding(e => e.RowVer));
        admAfter.RowVer.Should().Equal(admBefore.RowVer);
        (await AccountAsync(S0.Adm.AccountId)).PasswordHash.Should().Be(admAccountBefore.PasswordHash);
    }

    [Fact]
    public async Task IT_B1_031_Put_Profile_Without_Token_Returns_401()
    {
        var before = await SnapshotAsync();

        var r = await PutAsync(C + "Profile", null, new { DateOfBirth = new DateOnly(1995, 5, 20), Phone = "0901234567" });

        r.Code.Should().Be(401, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    // ---------------------------------------------------------------- 032–038

    [Fact]
    public async Task IT_B1_032_Change_Password_Temporary_Account_Skips_CurrentPassword()
    {
        var token = await TokenAsync(S0.Tmp);
        var before = await AccountAsync(S0.Tmp.AccountId);
        var lastAudit = await LastAuditIdAsync();

        var r = await PostAsync(C + "Profile/change-password", token,
            new { CurrentPassword = (string?)null, NewPassword = NewPw, ConfirmPassword = NewPw });

        r.Code.Should().Be(200, r.ToString());
        var after = await AccountAsync(S0.Tmp.AccountId);
        after.PasswordHash.Should().NotBe(before.PasswordHash);
        after.PasswordHash.Should().NotBe(NewPw, "không lưu mật khẩu dạng rõ");
        after.IsTemporaryPassword.Should().BeFalse();
        after.SecurityStamp.Should().NotBe(before.SecurityStamp);
        await AssertSingleAuditForAsync(lastAudit, S0.Tmp.AccountId);
        (await LoginAsync("tmp01", NewPw)).Code.Should().Be(200);
    }

    [Fact]
    public async Task IT_B1_033_Change_Password_Old_Password_Stops_Working_New_One_Works()
    {
        var token = await TokenAsync(S0.W1);
        var before = await AccountAsync(S0.W1.AccountId);

        var r = await PostAsync(C + "Profile/change-password", token,
            new { CurrentPassword = Pw, NewPassword = NewPw, ConfirmPassword = NewPw });

        r.Code.Should().Be(200, r.ToString());
        (await LoginAsync("wk01", Pw)).Code.Should().Be(401);
        (await LoginAsync("wk01", NewPw)).Code.Should().Be(200);
        var after = await AccountAsync(S0.W1.AccountId);
        after.PasswordHash.Should().NotBe(before.PasswordHash);
        after.IsTemporaryPassword.Should().BeFalse();
    }

    [Fact]
    public async Task IT_B1_034_Change_Password_Non_Temporary_Without_CurrentPassword_Returns_400()
    {
        var token = await TokenAsync(S0.W1);
        var before = await SnapshotAsync();

        var r = await PostAsync(C + "Profile/change-password", token,
            new { CurrentPassword = (string?)null, NewPassword = NewPw, ConfirmPassword = NewPw });

        r.Code.Should().Be(400, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_035_Change_Password_Wrong_CurrentPassword_Is_Rejected()
    {
        var token = await TokenAsync(S0.W1);
        var before = await SnapshotAsync();

        var r = await PostAsync(C + "Profile/change-password", token,
            new { CurrentPassword = "KhongDung1!", NewPassword = NewPw, ConfirmPassword = NewPw });

        r.Code.Should().BeOneOf(400, 401);
        Note($"IT_B1_035 sai mật khẩu hiện tại => {r.Code} {r.ErrorCode}");
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_036_Change_Password_Confirm_Mismatch_Returns_400()
    {
        var token = await TokenAsync(S0.W1);
        var before = await SnapshotAsync();

        var r = await PostAsync(C + "Profile/change-password", token,
            new { CurrentPassword = Pw, NewPassword = NewPw, ConfirmPassword = "N3w@Pass?" });

        r.Code.Should().Be(400, r.ToString());
        await AssertDbUnchangedAsync(before);
    }

    [Fact]
    public async Task IT_B1_037_Change_Password_Min_Length_Boundary()
    {
        var token = await TokenAsync(S0.W1);
        var tooShort = new string('a', AssumedMinPasswordLength - 1);
        var exactMin = "Aa1@" + new string('b', AssumedMinPasswordLength - 4);
        var before = await SnapshotAsync();

        var rShort = await PostAsync(C + "Profile/change-password", token,
            new { CurrentPassword = Pw, NewPassword = tooShort, ConfirmPassword = tooShort });

        rShort.Code.Should().Be(400, rShort.ToString());
        await AssertDbUnchangedAsync(before, "min − 1 không được đổi hash");

        var hashBefore = (await AccountAsync(S0.W1.AccountId)).PasswordHash;
        var rMin = await PostAsync(C + "Profile/change-password", token,
            new { CurrentPassword = Pw, NewPassword = exactMin, ConfirmPassword = exactMin });

        rMin.Code.Should().Be(200, rMin.ToString());
        (await AccountAsync(S0.W1.AccountId)).PasswordHash.Should().NotBe(hashBefore);
    }

    [Fact]
    public async Task IT_B1_038_Change_Password_New_Equals_Current_Is_Rejected()
    {
        var token = await TokenAsync(S0.W1);
        var before = await SnapshotAsync();

        var r = await PostAsync(C + "Profile/change-password", token,
            new { CurrentPassword = Pw, NewPassword = Pw, ConfirmPassword = Pw });

        r.Code.Should().Be(400, r.ToString());
        Note($"IT_B1_038 mật khẩu mới trùng cũ => {r.Code}");
        await AssertDbUnchangedAsync(before);
    }
}
