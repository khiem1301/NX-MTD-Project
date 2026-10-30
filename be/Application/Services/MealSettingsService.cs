using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed class MealSettingsService : IMealSettingsService
{
    /// <summary>Khung giờ chốt cho phép: trước giờ này bếp chưa kịp nhận số, sau giờ này suất ăn trưa không còn kịp chuẩn bị.</summary>
    public static readonly TimeOnly EarliestCutoff = new(5, 0);
    public static readonly TimeOnly LatestCutoff = new(11, 0);

    private static readonly TimeOnly DefaultReminder = new(8, 15);
    private static readonly TimeOnly DefaultCutoff = new(8, 30);

    private readonly IUnitOfWork _uow;
    private readonly ISystemSettingReader _reader;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;

    public MealSettingsService(IUnitOfWork uow, ISystemSettingReader reader, IAuditLogService audit, IClock clock)
    {
        _uow = uow; _reader = reader; _audit = audit; _clock = clock;
    }

    public static bool IsValidTime(string? value) => TryParse(value, out _);

    private static bool TryParse(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    public async Task<Result<MealSettingsDto>> GetAsync(CancellationToken ct) =>
        Result<MealSettingsDto>.Success(new MealSettingsDto(
            (await _reader.GetTimeAsync(SettingKeys.MealReminderTime, DefaultReminder, ct)).ToString("HH:mm"),
            (await _reader.GetTimeAsync(SettingKeys.MealCutoffTime, DefaultCutoff, ct)).ToString("HH:mm")));

    public async Task<Result<MealSettingsDto>> UpdateAsync(MealSettingsDto dto, CancellationToken ct)
    {
        if (!TryParse(dto.ReminderTime, out var reminder) || !TryParse(dto.CutoffTime, out var cutoff))
            return Result<MealSettingsDto>.Failure(ErrorCodes.CommonValidation, Messages.MealSettingsTimeInvalid, ResultKind.Validation);
        if (reminder >= cutoff)
            return Result<MealSettingsDto>.Failure(ErrorCodes.CommonValidation, Messages.MealSettingsOrder, ResultKind.Validation);
        if (cutoff < EarliestCutoff || cutoff > LatestCutoff)
            return Result<MealSettingsDto>.Failure(ErrorCodes.CommonValidation,
                string.Format(Messages.MealSettingsCutoffRange, EarliestCutoff.ToString("HH:mm"), LatestCutoff.ToString("HH:mm")),
                ResultKind.Validation);

        var before = (await GetAsync(ct)).Data!;
        await SaveAsync(SettingKeys.MealReminderTime, reminder.ToString("HH:mm"), "JOB-02 nhắc tổ trưởng xác nhận suất ăn", ct);
        await SaveAsync(SettingKeys.MealCutoffTime, cutoff.ToString("HH:mm"), "JOB-03 tự chốt suất ăn", ct);
        _audit.Add(new AuditEntry(nameof(SystemSetting), 0, AuditActions.Update, before,
            new MealSettingsDto(reminder.ToString("HH:mm"), cutoff.ToString("HH:mm")), "Cấu hình giờ chốt suất ăn"));
        await _uow.SaveChangesAsync();
        _reader.Invalidate(SettingKeys.MealReminderTime);
        _reader.Invalidate(SettingKeys.MealCutoffTime);
        return Result<MealSettingsDto>.Success(new MealSettingsDto(reminder.ToString("HH:mm"), cutoff.ToString("HH:mm")), Messages.MealSettingsSaved);
    }

    private async Task SaveAsync(string key, string value, string description, CancellationToken ct)
    {
        var row = await _uow.Repository<SystemSetting>().Query().FirstOrDefaultAsync(s => s.SettingKey == key, ct);
        if (row is null)
        {
            await _uow.Repository<SystemSetting>().AddAsync(new SystemSetting
            {
                SettingKey = key, SettingValue = value, Description = description, UpdatedAt = _clock.Now,
            });
            return;
        }
        row.SettingValue = value;
        row.UpdatedAt = _clock.Now;
    }
}
