namespace NX_MTD.Application.DTOs;

/// <summary>Giờ nhắc tổ trưởng xác nhận suất ăn (JOB-02) và giờ tự chốt suất ăn (JOB-03), dạng HH:mm.</summary>
public sealed record MealSettingsDto(string ReminderTime, string CutoffTime);
