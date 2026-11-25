using NX_MTD.Application.Interfaces;

namespace Integration.Tests.Infrastructure;

/// <summary>Đồng hồ cố định cho IT (T = 2026-11-24 theo Report-5.0).</summary>
public sealed class FixedClock : IClock
{
    public FixedClock(DateTime now)
    {
        Now = DateTime.SpecifyKind(now, DateTimeKind.Unspecified);
        Today = DateOnly.FromDateTime(Now);
    }

    public DateTime Now { get; }
    public DateOnly Today { get; }

    /// <summary>T = 2026-11-24 08:00 (VN).</summary>
    public static FixedClock Default { get; } = new(new DateTime(2026, 11, 24, 8, 0, 0));
}
