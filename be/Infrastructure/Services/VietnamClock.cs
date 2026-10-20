using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Services;

/// <summary>Giờ Việt Nam (UTC+7), khớp với dbo.fn_VnNow() mà DB dùng làm DEFAULT.</summary>
public sealed class VietnamClock : IClock
{
    private static readonly TimeZoneInfo VnZone = ResolveZone();

    public DateTime Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, VnZone).DateTime;

    public DateOnly Today => DateOnly.FromDateTime(Now);

    /// <summary>Windows dùng "SE Asia Standard Time", Linux dùng "Asia/Ho_Chi_Minh".</summary>
    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // thử id tiếp theo
            }
            catch (InvalidTimeZoneException)
            {
                // thử id tiếp theo
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("NX-MTD-VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam");
    }
}
