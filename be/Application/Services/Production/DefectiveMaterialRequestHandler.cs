using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Đơn NVL lỗi (DEFECTIVE_MATERIAL, tổ cắt → Quản đốc duyệt). Khi Quản đốc duyệt, hệ thống kiểm tồn kho NVL (swimlane):
/// đủ thì tự tạo đơn Xin NVL cho tổ cắt để Thủ kho cấp bù; thiếu thì tự tạo đơn từ "Mua NVL" cho Quản đốc (kèm NVL và số thiếu) để Quản đốc
/// tự tạo đơn mua (hệ thống không tự tạo đơn mua), phần đủ vẫn được cấp bù. Kết quả ghi vào <c>ResultNote</c> của đơn và báo Quản đốc (và Thủ kho khi có phiếu xin).
/// </summary>
public sealed class DefectiveMaterialRequestHandler : IRequestTypeHandler
{
    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly INotificationService _notifier;
    // IRequestService chứa chính các handler: lấy lúc cần để không vòng phụ thuộc.
    private readonly IServiceProvider _services;

    public DefectiveMaterialRequestHandler(IUnitOfWork uow, IStockService stock, INotificationService notifier,
        IServiceProvider services)
    {
        _uow = uow; _stock = stock; _notifier = notifier; _services = services;
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

        var (fromStock, toBuy) = await SplitByStockAsync(wanted, ct);
        var notes = new List<string>();
        if (fromStock.Count > 0) notes.Add(await StageRequisitionAsync(request, fromStock, info, ct));
        if (toBuy.Count > 0) notes.Add(await StagePurchaseRequestAsync(request, toBuy, info, ct));

        var summary = string.Join("; ", notes);
        request.ResultNote = summary.Length > 1000 ? summary[..1000] : summary;
        return Result.Success();
    }

    /// <summary>Kiểm tồn: phần kho cấp bù được ngay, phần còn lại phải mua.</summary>
    private async Task<(Dictionary<int, decimal> FromStock, Dictionary<int, decimal> ToBuy)> SplitByStockAsync(
        Dictionary<int, decimal> wanted, CancellationToken ct)
    {
        var fromStock = new Dictionary<int, decimal>();
        var toBuy = new Dictionary<int, decimal>();
        foreach (var (materialId, qty) in wanted)
        {
            var available = Math.Max(0, await _stock.GetAvailableAsync(StockKey.Material(materialId), ct));
            var give = Math.Min(qty, available);
            if (give > 0) fromStock[materialId] = give;
            if (qty - give > 0) toBuy[materialId] = qty - give;
        }
        return (fromStock, toBuy);
    }

    /// <summary>Đủ tồn: tạo đơn xin NVL để Thủ kho cấp bù cho tổ cắt. Trả về câu ghi vào kết quả đơn.</summary>
    private async Task<string> StageRequisitionAsync(Request request, Dictionary<int, decimal> fromStock,
        Dictionary<int, Material> info, CancellationToken ct)
    {
        var requests = _services.GetRequiredService<IRequestService>();
        var lines = fromStock.Select(kv => new RequestLineInput(kv.Key, null, null, null, kv.Value)).ToList();
        var draft = new RequestDraft(RequestTypes.MaterialRequisition, request.OrderId, request.WorkOrderId, null, null, null, null,
            null, $"Cấp bù NVL lỗi theo {request.RequestCode}", $"Tự tạo khi Quản đốc duyệt đơn NVL lỗi {request.RequestCode}.", lines, null);
        var requisition = await requests.StageSystemRequestAsync(draft, ct, request.CreatedByTeamId);
        await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
            "Cần cấp bù NVL lỗi", $"Quản đốc đã duyệt {request.RequestCode}: vui lòng lập phiếu cấp bù theo đơn xin {requisition.RequestCode}.",
            "/requests/inbox"), ct);
        return $"đủ tồn: đã tạo đơn xin NVL {requisition.RequestCode} ({Describe(fromStock, info)}), Thủ kho cấp bù cho tổ cắt";
    }

    /// <summary>
    /// Thiếu tồn: không tự tạo đơn mua mà tạo đơn từ "Mua NVL" cho Quản đốc (hệ thống tạo, báo Quản đốc theo vai trò), liệt kê NVL
    /// và số thiếu để Quản đốc tự tạo đơn mua. Trả về câu ghi vào kết quả đơn NVL lỗi.
    /// </summary>
    private async Task<string> StagePurchaseRequestAsync(Request request, Dictionary<int, decimal> toBuy,
        Dictionary<int, Material> info, CancellationToken ct)
    {
        var requests = _services.GetRequiredService<IRequestService>();
        var lines = toBuy.Select(kv => new RequestLineInput(kv.Key, null, null, null, kv.Value)).ToList();
        var shortage = Describe(toBuy, info);
        var draft = new RequestDraft(RequestTypes.MaterialPurchase, request.OrderId, request.WorkOrderId, null, null, null, null,
            null, $"Mua NVL bù hàng lỗi theo {request.RequestCode}",
            $"Tự tạo khi Quản đốc duyệt đơn NVL lỗi {request.RequestCode}: tồn kho không đủ, còn thiếu {shortage}. Vui lòng tạo đơn mua NVL.",
            lines, null);
        var purchase = await requests.StageSystemRequestAsync(draft, ct);
        return $"thiếu tồn: đã tạo đơn Mua NVL {purchase.RequestCode} ({shortage}), Quản đốc tạo đơn mua";
    }

    private static string Describe(IReadOnlyDictionary<int, decimal> qty, IReadOnlyDictionary<int, Material> info) =>
        string.Join(", ", qty.Select(kv =>
            $"{(info.TryGetValue(kv.Key, out var m) ? m.MaterialCode : kv.Key.ToString(CultureInfo.InvariantCulture))} {kv.Value:0.###}"));
}
