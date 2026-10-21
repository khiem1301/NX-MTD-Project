using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Common;

/// <summary>Việt Nam không có giờ mùa hè nên cộng cố định +7, giống dbo.fn_VnNow().
/// Làm tròn giây, Kind = Unspecified để khớp DATETIME2(0) (D21).</summary>
public sealed class VnClock : IClock
{
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    public DateTime Now
    {
        get
        {
            var vn = DateTime.UtcNow.Add(VnOffset);
            return new DateTime(vn.Year, vn.Month, vn.Day, vn.Hour, vn.Minute, vn.Second, DateTimeKind.Unspecified);
        }
    }

    public DateOnly Today => DateOnly.FromDateTime(Now);
}
