using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Admin cấu hình giờ nhắc / giờ chốt suất ăn: kiểm tra hợp lệ, lưu SystemSetting, ghi nhật ký, xóa cache đọc.</summary>
public class MealSettingsServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ISystemSettingReader _reader = Substitute.For<ISystemSettingReader>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly List<SystemSetting> _rows = [];

    public MealSettingsServiceTests()
    {
        _clock.Now.Returns(new DateTime(2026, 10, 28, 9, 0, 0));
        _uow.SaveChangesAsync().Returns(Task.FromResult(1));
        var repo = Substitute.For<IGenericRepository<SystemSetting>>();
        repo.Query().Returns(_ => _rows.AsTestAsyncQueryable());
        repo.When(r => r.AddAsync(Arg.Any<SystemSetting>())).Do(c => _rows.Add(c.Arg<SystemSetting>()));
        _uow.Repository<SystemSetting>().Returns(repo);
        _reader.GetTimeAsync(SettingKeys.MealReminderTime, Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>()).Returns(new TimeOnly(8, 15));
        _reader.GetTimeAsync(SettingKeys.MealCutoffTime, Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>()).Returns(new TimeOnly(8, 30));
    }

    private MealSettingsService Sut() => new(_uow, _reader, _audit, _clock);

    [Fact]
    public async Task Get_returnsTheCurrentTimesAsHHmm()
    {
        var result = await Sut().GetAsync(default);

        result.Data.Should().Be(new MealSettingsDto("08:15", "08:30"));
    }

    [Fact]
    public async Task Update_savesBothKeys_createsMissingRows_logsAndDropsTheReaderCache()
    {
        _rows.Add(new SystemSetting { SettingKey = SettingKeys.MealCutoffTime, SettingValue = "08:30" });   // MealReminder chưa có dòng

        var result = await Sut().UpdateAsync(new MealSettingsDto("07:45", "08:00"), default);

        result.IsSuccess.Should().BeTrue();
        _rows.Single(r => r.SettingKey == SettingKeys.MealCutoffTime).SettingValue.Should().Be("08:00");
        _rows.Single(r => r.SettingKey == SettingKeys.MealReminderTime).SettingValue.Should().Be("07:45");
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(SystemSetting) && e.Action == AuditActions.Update));
        _reader.Received(1).Invalidate(SettingKeys.MealCutoffTime);
        _reader.Received(1).Invalidate(SettingKeys.MealReminderTime);
    }

    [Theory]
    [InlineData("08:30", "08:30")]   // nhắc không sớm hơn chốt
    [InlineData("09:00", "08:30")]
    [InlineData("04:00", "04:30")]   // chốt quá sớm
    [InlineData("10:00", "11:30")]   // chốt quá muộn
    [InlineData("abc", "08:30")]
    [InlineData("08:15", "8h30")]
    public async Task Update_refusesAnInvalidOrOutOfRangeSetting_andChangesNothing(string reminder, string cutoff)
    {
        var result = await Sut().UpdateAsync(new MealSettingsDto(reminder, cutoff), default);

        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        _rows.Should().BeEmpty();
        _reader.DidNotReceive().Invalidate(Arg.Any<string>());
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public void Validator_requiresHHmmFormat()
    {
        var validator = new MealSettingsDtoValidator();

        validator.Validate(new MealSettingsDto("08:15", "08:30")).IsValid.Should().BeTrue();
        validator.Validate(new MealSettingsDto("8:15", "08:30")).IsValid.Should().BeFalse();
        validator.Validate(new MealSettingsDto("08:15", "25:00")).IsValid.Should().BeFalse();
        validator.Validate(new MealSettingsDto("", "08:30")).IsValid.Should().BeFalse();
    }
}
