using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Purchasing;

/// <summary>
/// Quyền ảnh chứng từ thanh toán đơn mua (OwnerType = PURCHASE_ORDER, mục đích PAYMENT_PROOF — DEV-KH-07-1):
/// Admin, Quản đốc, Kế toán xem được; chỉ Kế toán thêm / gỡ ảnh khi đơn đã đặt hàng và đang chờ thanh toán.
/// </summary>
public sealed class PurchaseOrderAttachmentAccess : IAttachmentOwnerAccess
{
    private const int MaxPhotos = 5;

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;

    public PurchaseOrderAttachmentAccess(IUnitOfWork uow, ICurrentUser user)
    {
        _uow = uow; _user = user;
    }

    public string OwnerType => AttachmentOwnerTypes.PurchaseOrder;

    public async Task<bool> CanReadAsync(int ownerId, CancellationToken ct) =>
        _user.Role is Roles.Admin or Roles.Manager or Roles.Accountant && await GetStatusAsync(ownerId, ct) is not null;

    public async Task<bool> CanWriteAsync(int ownerId, CancellationToken ct)
    {
        if (_user.Role != Roles.Accountant) return false;
        var status = await GetStatusAsync(ownerId, ct);
        return status is not null && PurchaseOrderFlowService.CanPay(status.GoodsStatus, status.PaymentStatus);
    }

    public Task<int> MaxFilesAsync(CancellationToken ct) => Task.FromResult(MaxPhotos);

    private sealed record Status(string GoodsStatus, string PaymentStatus);

    private Task<Status?> GetStatusAsync(int purchaseOrderId, CancellationToken ct) =>
        _uow.Repository<PurchaseOrder>().Query().AsNoTracking()
            .Where(p => p.PurchaseOrderId == purchaseOrderId)
            .Select(p => new Status(p.GoodsStatus, p.PaymentStatus))
            .FirstOrDefaultAsync(ct);
}
