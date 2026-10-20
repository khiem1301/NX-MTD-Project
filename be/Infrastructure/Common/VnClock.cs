using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Common;

/// <summary>Việt Nam không có giờ mùa hè nên cộng cố định +7, giống dbo.fn_VnNow().</summary>
public sealed class VnClock : IClock
{
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    public DateTime Now => DateTime.UtcNow.Add(VnOffset);
    public DateOnly Today => DateOnly.FromDateTime(Now);
}
