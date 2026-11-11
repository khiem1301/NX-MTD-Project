using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-94 / DEV-ALL-95: số phiếu nhập đã xác nhận không vượt số lần giao dự kiến khi Quản đốc chưa quyết định có giao thêm hay
/// không. Số lần giao dùng chung cho cả đơn: đơn mua khai ở đầu đơn mua, khách cấp khai ở đơn hàng (mặc định 1). Một phiếu thường
/// (không phải bổ sung) đã xác nhận của đơn là một lần giao, dù phiếu có những NVL nào. Đủ số lần thì chặn phiếu thường của đơn;
/// mọi NVL còn thiếu (kể cả NVL chưa về lần nào) vào đơn thiếu NVL và hàng giao thêm chỉ nhận bằng phiếu bổ sung. Phiếu bổ sung đếm
/// theo số lần giao bù Quản đốc khai trên đơn thiếu NVL (<c>Request.ExpectedDeliveryCount</c>, cộng dồn; đơn cũ chưa khai thì không
/// giới hạn). Chỉ đếm phiếu ĐÃ XÁC NHẬN nên kiểm cả lúc lập / sửa phiếu và lúc xác nhận.
/// </summary>
public sealed class DeliveryCountGuard
{
    private readonly IUnitOfWork _uow;

    public DeliveryCountGuard(IUnitOfWork uow) => _uow = uow;

    private IQueryable<MaterialReceipt> Confirmed =>
        _uow.Repository<MaterialReceipt>().Query().AsNoTracking().Where(r => r.Status == ReceiptStatuses.Confirmed);

    /// <summary>Kiểm phiếu đang chờ xác nhận theo loại phiếu (bổ sung / theo đơn mua / khách cấp). Null nếu còn lượt giao.</summary>
    public Task<string?> CheckReceiptAsync(MaterialReceipt receipt, CancellationToken ct)
    {
        if (receipt.ShortageRequestId is { } requestId) return CheckReplenishAsync(requestId, ct);
        if (receipt.PurchaseOrderId is { } poId) return CheckPurchaseOrderAsync(poId, receipt.MaterialReceiptId, ct);
        return receipt.OrderId is { } orderId
            ? CheckCustomerAsync(orderId, receipt.MaterialReceiptId, ct)
            : Task.FromResult<string?>(null);
    }

    /// <summary>Phiếu thường theo đơn mua: đơn mua đã nhận đủ số lần NCC dự kiến giao thì chặn.</summary>
    public async Task<string?> CheckPurchaseOrderAsync(int purchaseOrderId, int? exceptReceiptId, CancellationToken ct)
    {
        var po = await _uow.Repository<PurchaseOrder>().Query().AsNoTracking().Where(p => p.PurchaseOrderId == purchaseOrderId)
            .Select(p => new { p.PoCode, p.ExpectedDeliveryCount }).FirstAsync(ct);
        var expected = po.ExpectedDeliveryCount ?? 1;
        return await PurchaseOrderDeliveriesAsync(purchaseOrderId, exceptReceiptId, ct) >= expected
            ? string.Format(WarehouseMessages.DeliveryCountReached, expected, po.PoCode)
            : null;
    }

    /// <summary>Phiếu thường khách cấp: đơn hàng đã nhận đủ số lần khách dự kiến giao thì chặn.</summary>
    public async Task<string?> CheckCustomerAsync(int orderId, int? exceptReceiptId, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking().Where(o => o.OrderId == orderId)
            .Select(o => new { o.OrderCode, o.CustomerDeliveryCount }).FirstAsync(ct);
        var expected = order.CustomerDeliveryCount ?? 1;
        return await CustomerDeliveriesAsync(orderId, exceptReceiptId, ct) >= expected
            ? string.Format(WarehouseMessages.DeliveryCountReached, expected, order.OrderCode)
            : null;
    }

    /// <summary>Số lần đơn mua đã giao = số phiếu thường đã xác nhận của đơn mua (bỏ qua một phiếu nếu cần).</summary>
    public Task<int> PurchaseOrderDeliveriesAsync(int purchaseOrderId, int? exceptReceiptId, CancellationToken ct) =>
        Confirmed.CountAsync(r => r.PurchaseOrderId == purchaseOrderId && r.ShortageRequestId == null
                                  && r.MaterialReceiptId != exceptReceiptId, ct);

    /// <summary>Số lần khách đã giao NVL của đơn = số phiếu khách cấp thường đã xác nhận của đơn (bỏ qua một phiếu nếu cần).</summary>
    public Task<int> CustomerDeliveriesAsync(int orderId, int? exceptReceiptId, CancellationToken ct) =>
        Confirmed.CountAsync(r => r.OrderId == orderId && r.PurchaseOrderId == null && r.ShortageRequestId == null
                                  && r.MaterialReceiptId != exceptReceiptId, ct);

    /// <summary>Phiếu bổ sung: đã nhận đủ số lần giao bù Quản đốc khai trên đơn thiếu NVL thì chặn.</summary>
    public async Task<string?> CheckReplenishAsync(int shortageRequestId, CancellationToken ct)
    {
        var request = await _uow.Repository<Request>().Query().AsNoTracking().Where(q => q.RequestId == shortageRequestId)
            .Select(q => new { q.RequestCode, q.ExpectedDeliveryCount }).FirstAsync(ct);
        if (request.ExpectedDeliveryCount is not { } expected) return null;
        return await ReplenishCountAsync(shortageRequestId, ct) >= expected
            ? string.Format(WarehouseMessages.ReplenishCountReached, expected, request.RequestCode)
            : null;
    }

    /// <summary>Số lần đã giao bù = số phiếu bổ sung đã xác nhận của đơn thiếu NVL.</summary>
    public Task<int> ReplenishCountAsync(int shortageRequestId, CancellationToken ct) =>
        Confirmed.CountAsync(r => r.ShortageRequestId == shortageRequestId, ct);
}
