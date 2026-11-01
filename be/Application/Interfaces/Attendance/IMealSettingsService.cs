using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>Admin cấu hình giờ nhắc và giờ chốt suất ăn (SystemSetting MEAL_REMINDER_TIME, MEAL_CUTOFF_TIME).</summary>
public interface IMealSettingsService
{
    Task<Result<MealSettingsDto>> GetAsync(CancellationToken ct);

    /// <summary>Giờ nhắc phải sớm hơn giờ chốt; giờ chốt trong khung hợp lý. Có hiệu lực ngay cho lần kiểm tra / chạy job kế tiếp.</summary>
    Task<Result<MealSettingsDto>> UpdateAsync(MealSettingsDto dto, CancellationToken ct);
}
