using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Đơn giá mẫu cho test giao việc: công đoạn có đơn giá được duyệt (hiệu lực từ đầu năm) mới giao được cho công nhân (DEV-ALL-73).</summary>
internal static class TestRates
{
    public static readonly DateOnly EffectiveSince = new(2026, 1, 1);

    public static PieceRate Approved(int operationId, string status = PieceRateStatuses.Approved) => new()
    {
        OperationId = operationId, Rate = 100, Status = status, EffectiveDate = EffectiveSince,
    };
}
