using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Ngày dự kiến giao của đơn mua: không trước hôm nay, không trước ngày đặt, không sau hạn giao của đơn hàng gắn kèm.</summary>
internal static class PurchaseDeliveryDate
{
    /// <summary>Trả về thông báo lỗi, null nếu hợp lệ. Chưa nhập ngày thì không kiểm ở đây (bắt buộc kiểm ở validator).</summary>
    public static async Task<string?> CheckAsync(IUnitOfWork uow, IClock clock, DateOnly? expected, DateOnly orderDate,
        int? orderId, CancellationToken ct)
    {
        if (expected is not { } date) return null;
        DateOnly? due = orderId is { } id
            ? await uow.Repository<CustomerOrder>().Query().AsNoTracking()
                .Where(o => o.OrderId == id).Select(o => (DateOnly?)o.DueDate).FirstOrDefaultAsync(ct)
            : null;
        return DocumentDateRules.CheckExpectedDelivery(date, clock.Today, orderDate, due);
    }
}
