using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.Services.Production;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Hạn sửa (GBR-09), giờ hết ca, tổ của người đăng nhập, khóa tháng (KI-11).</summary>
public class ProductionGuardTests : ProductionTestBase
{
    // ---------- Hạn sửa: hết ngày làm việc kế tiếp, bỏ Chủ nhật ----------

    [Theory]
    [InlineData(2026, 10, 21, 2026, 10, 22)]   // thứ Tư → hết thứ Năm
    [InlineData(2026, 10, 23, 2026, 10, 24)]   // thứ Sáu → hết thứ Bảy (thứ Bảy vẫn là ngày làm việc)
    [InlineData(2026, 10, 24, 2026, 10, 26)]   // thứ Bảy → bỏ Chủ nhật, hết thứ Hai
    [InlineData(2026, 10, 18, 2026, 10, 19)]   // Chủ nhật → hết thứ Hai
    public void NextWorkingDayEnd_SkipsSunday_AndEndsAtLastSecondOfTheDay(int y, int m, int d, int ey, int em, int ed)
    {
        var end = ProductionGuard.NextWorkingDayEnd(new DateOnly(y, m, d));

        end.Should().Be(new DateTime(ey, em, ed, 23, 59, 59));
    }

    // ---------- Giờ hết ca (QA/QC) ----------

    [Fact]
    public async Task ShiftEnd_Morning_IsLunchStartFromSettings()
    {
        var end = await Guard().ShiftEndAsync(Today, ShiftCodes.Morning, default);

        end.Should().Be(new DateTime(2026, 10, 21, 11, 30, 0));
    }

    [Fact]
    public async Task ShiftEnd_Afternoon_IsWorkEndFromSettings()
    {
        var end = await Guard().ShiftEndAsync(Today, ShiftCodes.Afternoon, default);

        end.Should().Be(new DateTime(2026, 10, 21, 16, 30, 0));
    }

    [Fact]
    public async Task ShiftEnd_Overtime_IsEndOfThatDay()
    {
        var end = await Guard().ShiftEndAsync(Today, ShiftCodes.Overtime, default);

        end.Should().Be(new DateTime(2026, 10, 21, 23, 59, 59));
    }

    // ---------- Cửa sổ sửa (UC22, MSG20) ----------

    [Fact]
    public void CheckEditWindow_WithinDeadline_AllowsEveryone()
    {
        AsTeamLeader();

        Guard().CheckEditWindow(Now.AddHours(1), null).Should().BeNull();
    }

    [Fact]
    public void CheckEditWindow_PastDeadline_TeamLeaderIsForbidden()
    {
        AsTeamLeader();

        var result = Guard().CheckEditWindow(Now.AddMinutes(-1), "xin sửa");

        result!.ErrorCode.Should().Be(ErrorCodes.ProductionEditDeadlinePassed);
        result.Kind.Should().Be(ResultKind.Forbidden);
    }

    [Fact]
    public void CheckEditWindow_PastDeadline_QaQcIsForbidden()
    {
        AsQaQc();

        Guard().CheckEditWindow(Now.AddMinutes(-1), "lý do")!.ErrorCode
            .Should().Be(ErrorCodes.ProductionEditDeadlinePassed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void CheckEditWindow_PastDeadline_ManagerNeedsReason(string? reason)
    {
        AsManager();

        var result = Guard().CheckEditWindow(Now.AddMinutes(-1), reason);

        result!.Kind.Should().Be(ResultKind.Validation);
        result.Message.Should().Be(Messages.ReasonRequired);
    }

    [Fact]
    public void CheckEditWindow_PastDeadline_ManagerWithReasonIsAllowed()
    {
        AsManager();

        Guard().CheckEditWindow(Now.AddMinutes(-1), "Nhập thiếu do mất điện").Should().BeNull();
    }

    // ---------- Tổ của người đăng nhập ----------

    [Fact]
    public void ResolveSewingTeam_TeamLeader_UsesOwnTeamAndIgnoresRequested()
    {
        AsTeamLeader(teamId: 5);

        var result = Guard().ResolveSewingTeam(99);

        result.Data.Should().Be(5);   // không cho tổ trưởng sửa hộ tổ khác bằng cách gửi SewingTeamId
    }

    [Fact]
    public void ResolveSewingTeam_Deputy_IsTreatedLikeLeader()
    {
        AsTeamLeader(teamId: 5, role: Roles.TeamDeputy);

        Guard().ResolveSewingTeam(null).Data.Should().Be(5);
    }

    [Fact]
    public void ResolveSewingTeam_CuttingTeam_IsForbidden()
    {
        AsCuttingTeamLeader();

        var result = Guard().ResolveSewingTeam(null);

        result.Kind.Should().Be(ResultKind.Forbidden);
        result.ErrorCode.Should().Be(ErrorCodes.ProductionForbidden);
    }

    [Fact]
    public void ResolveSewingTeam_Manager_MustChooseTeam()
    {
        AsManager();

        Guard().ResolveSewingTeam(null).IsSuccess.Should().BeFalse();
        Guard().ResolveSewingTeam(0).IsSuccess.Should().BeFalse();
        Guard().ResolveSewingTeam(7).Data.Should().Be(7);
    }

    // ---------- Khóa tháng (GBR-10) ----------

    [Fact]
    public async Task IsPeriodLocked_DateInsideLockedPeriod_IsTrue()
    {
        SetupQueryable(new List<AccountingPeriod>
        {
            new() { PeriodMonth = "2026-10", Status = RecordStatus.Locked, StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 31) },
            new() { PeriodMonth = "2026-11", Status = RecordStatus.Open, StartDate = new DateOnly(2026, 11, 1), EndDate = new DateOnly(2026, 11, 30) },
        });

        (await Guard().IsPeriodLockedAsync(new DateOnly(2026, 10, 15), default)).Should().BeTrue();
        (await Guard().IsPeriodLockedAsync(new DateOnly(2026, 11, 15), default)).Should().BeFalse();   // tháng mở
        (await Guard().IsPeriodLockedAsync(new DateOnly(2026, 12, 15), default)).Should().BeFalse();   // chưa có kỳ
    }
}
