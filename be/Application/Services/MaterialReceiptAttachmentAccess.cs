using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Quyền ảnh chứng từ phiếu nhập NVL (OwnerType = MATERIAL_RECEIPT, mục đích DOCUMENT_PHOTO — UC37 / TH-03):
/// Thủ kho, Quản đốc, Admin xem được; chỉ Thủ kho thêm / gỡ ảnh khi phiếu còn chờ xác nhận hoặc bị trả lại.
/// </summary>
public sealed class MaterialReceiptAttachmentAccess : IAttachmentOwnerAccess
{
    private const int MaxPhotos = 5;

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;

    public MaterialReceiptAttachmentAccess(IUnitOfWork uow, ICurrentUser user)
    {
        _uow = uow; _user = user;
    }

    public string OwnerType => AttachmentOwnerTypes.MaterialReceipt;

    public async Task<bool> CanReadAsync(int ownerId, CancellationToken ct) =>
        _user.Role is Roles.WarehouseKeeper or Roles.Manager or Roles.Admin && await GetStatusAsync(ownerId, ct) is not null;

    public async Task<bool> CanWriteAsync(int ownerId, CancellationToken ct)
    {
        if (_user.Role != Roles.WarehouseKeeper) return false;
        var status = await GetStatusAsync(ownerId, ct);
        return status is ReceiptStatuses.Pending or ReceiptStatuses.Returned;
    }

    public Task<int> MaxFilesAsync(CancellationToken ct) => Task.FromResult(MaxPhotos);

    private Task<string?> GetStatusAsync(int receiptId, CancellationToken ct) =>
        _uow.Repository<MaterialReceipt>().Query().AsNoTracking()
            .Where(r => r.MaterialReceiptId == receiptId)
            .Select(r => (string?)r.Status)
            .FirstOrDefaultAsync(ct);
}
