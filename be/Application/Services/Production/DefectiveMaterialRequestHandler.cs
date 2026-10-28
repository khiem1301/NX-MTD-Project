using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Production;

/// <summary>
/// Đơn NVL lỗi (DEFECTIVE_MATERIAL, tổ cắt → Quản đốc duyệt). Khi Quản đốc duyệt, hệ thống kiểm tồn kho NVL (swimlane):
/// đủ thì tự tạo đơn Xin NVL cho tổ cắt để Thủ kho cấp bù; thiếu thì tự tạo đơn mua (chờ chốt) cho phần thiếu, phần đủ vẫn
/// được cấp bù. Kết quả ghi vào <c>ResultNote</c> của đơn và báo Quản đốc (và Thủ kho khi có phiếu xin).
/// </summary>
public sealed class DefectiveMaterialRequestHandler : IRequestTypeHandler
{
    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly IDocumentCodeGenerator _codes;
    private readonly INotificationService _notifier;
    private readonly IClock _clock;
    // IRequestService chứa chính các handler: lấy lúc cần để không vòng phụ thuộc.
    private readonly IServiceProvider _services;

    public DefectiveMaterialRequestHandler(IUnitOfWork uow, IStockService stock, IDocumentCodeGenerator codes,
        INotificationService notifier, IClock clock, IServiceProvider services)
    {
        _uow = uow; _stock = stock; _codes = codes; _notifier = notifier; _clock = clock; _services = services;
    }

    public string RequestType => RequestTypes.DefectiveMaterial;

    public bool AllowManualComplete => true;

    public Task<Result> ValidateAsync(RequestDraft draft, CancellationToken ct) => Task.FromResult(Result.Success());

    public async Task<Result> OnStatusChangedAsync(Request request, string oldStatus, CancellationToken ct)
    {
        if (request.Status != RequestStatuses.Approved || oldStatus != RequestStatuses.PendingApproval) return Result.Success();

        var wanted = (await _uow.Repository<RequestLine>().Query().AsNoTracking()
                .Where(l => l.RequestId == request.RequestId && l.MaterialId != null)
                .Select(l => new { MaterialId = l.MaterialId!.Value, l.Quantity }).ToListAsync(ct))
            .GroupBy(l => l.MaterialId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        if (wanted.Count == 0) return Result.Success();

        var info = await _uow.Repository<Material>().Query().AsNoTracking().Where(m => wanted.Keys.Contains(m.MaterialId))
            .ToDictionaryAsync(m => m.MaterialId, ct);

        // Kiểm tồn: phần kho cấp bù được ngay, phần còn lại phải mua.
        var fromStock = new Dictionary<int, decimal>();
        var toBuy = new Dictionary<int, decimal>();
        foreach (var (materialId, qty) in wanted)
        {
            var available = Math.Max(0, await _stock.GetAvailableAsync(StockKey.Material(materialId), ct));
            var give = Math.Min(qty, available);
            if (give > 0) fromStock[materialId] = give;
            if (qty - give > 0) toBuy[materialId] = qty - give;
        }

        var notes = new List<string>();
        var requests = _services.GetRequiredService<IRequestService>();

        if (fromStock.Count > 0)
        {
            var lines = fromStock.Select(kv => new RequestLineInput(kv.Key, null, null, null, kv.Value)).ToList();
            var draft = new RequestDraft(RequestTypes.MaterialRequisition, request.OrderId, request.WorkOrderId, null, null, null, null,
                null, $"Cấp bù NVL lỗi theo {request.RequestCode}", $"Tự tạo khi Quản đốc duyệt đơn NVL lỗi {request.RequestCode}.", lines, null);
            var requisition = await requests.StageSystemRequestAsync(draft, ct, request.CreatedByTeamId);
            notes.Add($"đủ tồn: đã tạo đơn xin NVL {requisition.RequestCode} ({Describe(fromStock, info)}), Thủ kho cấp bù cho tổ cắt");
            await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
                "Cần cấp bù NVL lỗi", $"Quản đốc đã duyệt {request.RequestCode}: vui lòng lập phiếu cấp bù theo đơn xin {requisition.RequestCode}.",
                "/requests/inbox"), ct);
        }

        if (toBuy.Count > 0)
        {
            var orderMaterials = request.OrderId is { } orderId
                ? await _uow.Repository<OrderMaterial>().Query().AsNoTracking()
                    .Where(om => om.OrderId == orderId && toBuy.Keys.Contains(om.MaterialId))
                    .ToDictionaryAsync(om => om.MaterialId, om => om.OrderMaterialId, ct)
                : new Dictionary<int, int>();
            var po = new PurchaseOrder
            {
                PoCode = await _codes.NextAsync<PurchaseOrder>(DocumentPrefixes.PurchaseOrder, p => p.PoCode, ct),
                OrderId = request.OrderId,
                SourceRequestId = request.RequestId,
                GoodsStatus = PurchaseOrderStatuses.PendingFinalisation,
                PaymentStatus = PurchaseOrderStatuses.Unpaid,
                Note = $"Tự tạo khi duyệt NVL lỗi {request.RequestCode}: tồn kho không đủ.",
                CreatedAt = _clock.Now,
                PurchaseOrderLines = toBuy.Select(kv => orderMaterials.TryGetValue(kv.Key, out var omId)
                    ? new PurchaseOrderLine { OrderMaterialId = omId, OrderedQty = kv.Value }
                    : new PurchaseOrderLine { MaterialId = kv.Key, OrderedQty = kv.Value }).ToList(),
            };
            await _uow.Repository<PurchaseOrder>().AddAsync(po);
            notes.Add($"thiếu tồn: đã tạo đơn mua {po.PoCode} chờ chốt ({Describe(toBuy, info)}), Quản đốc đặt hàng với nhà cung cấp");
            await _notifier.NotifyRoleAsync(RoleCodes.Manager, new NotificationMessage(NotificationSources.Warehouse,
                "Đơn mua cho NVL lỗi chờ chốt", $"Tồn kho không đủ bù NVL lỗi của {request.RequestCode}: đơn mua {po.PoCode} chờ bạn chốt nhà cung cấp và đơn giá.",
                "/purchase/orders"), ct);
        }

        var summary = string.Join("; ", notes);
        request.ResultNote = summary.Length > 1000 ? summary[..1000] : summary;
        return Result.Success();
    }

    private static string Describe(IReadOnlyDictionary<int, decimal> qty, IReadOnlyDictionary<int, Material> info) =>
        string.Join(", ", qty.Select(kv =>
            $"{(info.TryGetValue(kv.Key, out var m) ? m.MaterialCode : kv.Key.ToString(CultureInfo.InvariantCulture))} {kv.Value:0.###}"));
}
