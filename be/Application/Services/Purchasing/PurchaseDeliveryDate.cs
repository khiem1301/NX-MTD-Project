using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Ngày dự kiến giao của đơn mua: không trước hôm nay, không trước ngày đặt, không sau hạn giao của đơn hàng gắn kèm.
/// Giao từ 2 lần trở lên thì ngày giao đến cũng theo quy tắc đó.
/// </summary>
internal static class PurchaseDeliveryDate
{
    /// <summary>Ngày giao đến lưu vào đơn mua: chỉ có khi giao từ 2 lần trở lên.</summary>
    public static DateOnly? DateTo(short count, DateOnly? dateTo) => DocumentDateRules.IsRangeDelivery(count) ? dateTo : null;

    /// <summary>Trả về thông báo lỗi, null nếu hợp lệ. Chưa nhập ngày thì không kiểm ở đây (bắt buộc kiểm ở validator).</summary>
    public static async Task<string?> CheckAsync(IUnitOfWork uow, IClock clock, DateOnly? expected, DateOnly? expectedTo,
        DateOnly orderDate, int? orderId, CancellationToken ct)
    {
        var dates = new[] { expected, expectedTo }.OfType<DateOnly>().ToList();
        if (dates.Count == 0) return null;
        DateOnly? due = orderId is { } id
            ? await uow.Repository<CustomerOrder>().Query().AsNoTracking()
                .Where(o => o.OrderId == id).Select(o => (DateOnly?)o.DueDate).FirstOrDefaultAsync(ct)
            : null;
        return dates.Select(d => DocumentDateRules.CheckExpectedDelivery(d, clock.Today, orderDate, due))
            .FirstOrDefault(e => e is not null);
    }
}
