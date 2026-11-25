using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Integration.Tests.Infrastructure;
using Integration.Tests.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.DTOs;
using NX_MTD.API.Common;

namespace Integration.Tests.Smoke;

/// <summary>
/// Smoke: API → Service → Test DB thật + xác nhận Rollback xoá sạch dữ liệu seed.
/// </summary>
public sealed class InfrastructureSmokeTests : BaseIntegrationTest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task IT001_Login_Through_Api_Service_Db_Then_Rollback_Leaves_Db_Clean()
    {
        // Arrange — seed nằm trong InitializeAsync; ID động từ Ctx (không hardcode).
        var accountId = Ctx.Admin01.AccountId;
        var employeeId = Ctx.Admin01.EmployeeId;
        var username = Ctx.Admin01.Username;

        accountId.Should().BeGreaterThan(0);
        username.Should().Be("admin_01");

        // Act — HTTP → AuthController → AuthService → DbContext/SQL Server
        using var response = await Client.PostAsJsonAsync(
            "/api/v1/common/auth/login",
            new LoginDto(username, TestContext.TestPassword));

        // Assert HTTP + JSON
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ApiSuccess<LoginResultDto>>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();
        payload.Data.Should().NotBeNull();
        payload.Data!.User.AccountId.Should().Be(accountId);
        payload.Data.User.EmployeeId.Should().Be(employeeId);
        payload.Data.User.Username.Should().Be(username);
        payload.Data.User.Role.Should().Be("Admin");
        payload.Data.AccessToken.Should().NotBeNullOrWhiteSpace();

        // Assert DB trong ambient transaction vẫn thấy bản ghi seed
        var existsInTx = await Db.Accounts.AsNoTracking()
            .AnyAsync(a => a.AccountId == accountId && a.Username == username);
        existsInTx.Should().BeTrue("seed phải tồn tại trong transaction đang mở");

        // Act — Rollback ambient (mô phỏng DisposeAsync của BaseIntegrationTest)
        await Factory.RollbackAmbientTransactionAsync();

        // Assert — connection mới (góc nhìn committed) không còn tài khoản IT
        await using var verify = new SqlConnection(Factory.DbState.Connection.ConnectionString);
        await verify.OpenAsync();
        await using var cmd = verify.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM dbo.Account WHERE Username = @u";
        cmd.Parameters.AddWithValue("@u", username);
        var count = (int)(await cmd.ExecuteScalarAsync())!;
        count.Should().Be(0, "sau Rollback, dữ liệu seed IT không được để sót trên DB");
    }
}
