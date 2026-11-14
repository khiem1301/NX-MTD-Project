using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Giới hạn dòng của đơn Xin NVL / Xin BTP (DEV-ALL-77). FE giữ cùng giá trị ở <c>features/requests/constants.ts</c>.</summary>
public static class RequisitionLimits
{
    public const int MaxLines = 50;

    /// <summary>NVL: tối đa 9.999.999 mỗi dòng.</summary>
    public const decimal MaxMaterialQty = 9_999_999m;

    /// <summary>Chỉ vải mới có số lẻ, tối đa 2 chữ số sau dấu phẩy; NVL khác vải là số nguyên.</summary>
    public const int FabricDecimals = 2;

    /// <summary>BTP đếm theo bộ (số nguyên), cùng trần số lượng hàng chiếc 1.000.000.</summary>
    public const decimal MaxPieceQty = 1_000_000m;
}

/// <summary>
/// Kiểm chung của đơn Xin NVL / Xin BTP của tổ trưởng: có dòng, tối đa 50 dòng, không trùng, ngày cần không ở quá khứ.
/// Đơn Xin NVL / Xin BTP còn kiểm trần số lượng mỗi dòng là số khả dụng trong kho (DEV-ALL-110, <see cref="CheckStock"/>).
/// </summary>
public abstract class RequisitionRequestHandler : IRequestTypeHandler
{
    private readonly IClock _clock;

    protected RequisitionRequestHandler(IClock clock) => _clock = clock;

    public abstract string RequestType { get; }

    public bool AllowManualComplete => true;

    /// <summary>Kiểm từng dòng theo loại đơn (tên hàng, kiểu số lượng); chạy sau khi kiểm chung đạt.</summary>
    protected abstract Task<Result> CheckLinesAsync(RequestDraft draft, CancellationToken ct);

    public async Task<Result> ValidateAsync(RequestDraft draft, CancellationToken ct)
    {
        if (draft.Lines.Count == 0) return Invalid(Messages.RequisitionLinesRequired);
        if (draft.Lines.Count > RequisitionLimits.MaxLines)
            return Invalid(string.Format(Messages.RequisitionTooManyLines, RequisitionLimits.MaxLines));
        if (draft.RequestId is null && draft.TargetDate is { } target && target < _clock.Today)
            return Invalid(Messages.RequisitionTargetDatePast);
        if (draft.Lines.Any(l => l.MaterialId is null && l.OrderLineId is null && string.IsNullOrWhiteSpace(l.ItemDescription)))
            return Invalid(Messages.RequisitionItemRequired);

        var keys = draft.Lines.Select(l => l.MaterialId is { } m ? $"M{m}" : l.OrderLineId is { } o ? $"O{o}"
            : $"T{l.ItemDescription!.Trim().ToUpperInvariant()}");
        if (keys.GroupBy(k => k).Any(g => g.Count() > 1)) return Invalid(Messages.RequisitionDuplicateItem);

        return await CheckLinesAsync(draft, ct);
    }

    public Task<Result> OnStatusChangedAsync(Request request, string oldStatus, CancellationToken ct) =>
        Task.FromResult(Result.Success());

    protected static Result Invalid(string message) =>
        Result.Failure(ErrorCodes.RequestLinesInvalid, message, ResultKind.Validation);

    /// <summary>Mỗi dòng không vượt khả dụng trong kho; liệt kê mọi dòng vượt trong một lỗi (DEV-ALL-110).</summary>
    protected static Result CheckStock(IEnumerable<(string Name, decimal Quantity, decimal Available)> lines)
    {
        var over = lines.Where(l => l.Quantity > Math.Max(0, l.Available))
            .Select(l => string.Format(Messages.RequisitionOverStockLine, l.Name, Qty(l.Quantity), Qty(Math.Max(0, l.Available))))
            .ToList();
        return over.Count == 0 ? Result.Success() : Invalid(string.Format(Messages.RequisitionOverStock, string.Join("; ", over)));
    }

    protected static string Qty(decimal value) => value.ToString("#,0.##", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));
}

/// <summary>
/// Xin NVL (tổ cắt, tổ may): mỗi dòng phải chọn NVL trong danh mục đang dùng. Vải: số lượng lớn hơn 0, tối đa 2 chữ số sau dấu phẩy;
/// NVL khác vải (chỉ, cúc, phụ liệu…): số nguyên. Tối đa 9.999.999 mỗi dòng.
/// Có chọn lệnh cắt (tổ cắt, DEV-ALL-92): chỉ xin vải hệ thống tính cho lệnh, tổng các đơn xin NVL của lệnh không vượt trần của lệnh.
/// Tổ may bắt buộc chọn đơn hàng và chỉ xin NVL của đơn; mọi dòng không vượt khả dụng kho (DEV-ALL-110).
/// </summary>
public sealed class MaterialRequisitionRequestHandler : RequisitionRequestHandler
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IWorkOrderFabricNeeds _needs;
    private readonly IStockService _stock;

    public MaterialRequisitionRequestHandler(IClock clock, IUnitOfWork uow, ICurrentUser user, IWorkOrderFabricNeeds needs,
        IStockService stock) : base(clock)
    {
        _uow = uow; _user = user; _needs = needs; _stock = stock;
    }

    public override string RequestType => RequestTypes.MaterialRequisition;

    /// <summary>Số lượng hợp lệ của một NVL: vải được lẻ 2 số, loại khác chỉ số nguyên.</summary>
    public static bool IsQuantityValid(bool isFabric, decimal quantity) =>
        quantity > 0 && quantity <= RequisitionLimits.MaxMaterialQty
        && (isFabric ? decimal.Round(quantity, RequisitionLimits.FabricDecimals) == quantity : quantity == decimal.Truncate(quantity));

    protected override async Task<Result> CheckLinesAsync(RequestDraft draft, CancellationToken ct)
    {
        if (draft.Lines.Any(l => l.MaterialId is null)) return Invalid(Messages.RequisitionMaterialRequired);

        var ids = draft.Lines.Select(l => l.MaterialId!.Value).Distinct().ToList();
        var materials = await _uow.Repository<Material>().Query().AsNoTracking()
            .Where(m => ids.Contains(m.MaterialId) && m.Status == MaterialStatuses.Active && m.MaterialType != MaterialTypes.Scrap)
            .Select(m => new { m.MaterialId, m.MaterialName, m.MaterialType }).ToDictionaryAsync(m => m.MaterialId, ct);

        foreach (var line in draft.Lines)
        {
            if (!materials.TryGetValue(line.MaterialId!.Value, out var material))
                return Invalid(Messages.RequisitionMaterialInvalid);
            var isFabric = material.MaterialType == MaterialTypes.Fabric;
            if (!IsQuantityValid(isFabric, line.Quantity))
                return Invalid(string.Format(isFabric ? Messages.RequisitionFabricQtyInvalid : Messages.RequisitionOtherQtyInvalid,
                    material.MaterialName));
        }

        var names = materials.ToDictionary(m => m.Key, m => m.Value.MaterialName);
        var scope = await CheckOrderScopeAsync(draft, names, ct);
        if (!scope.IsSuccess) return scope;
        var available = await _stock.GetMaterialAvailableAsync(ids, ct);
        var stock = CheckStock(draft.Lines.Select(l => (names[l.MaterialId!.Value], l.Quantity, available.GetValueOrDefault(l.MaterialId!.Value))));
        if (!stock.IsSuccess) return stock;
        return draft.WorkOrderId is int workOrderId ? await CheckWorkOrderCapAsync(draft, workOrderId, names, ct) : Result.Success();
    }

    /// <summary>
    /// Tổ may bắt buộc chọn đơn hàng (DEV-ALL-110). Có đơn hàng mà không theo lệnh cắt thì chỉ xin NVL đã khai trong NVL của đơn,
    /// khớp phạm vi phiếu cấp (DEV-ALL-99).
    /// </summary>
    private async Task<Result> CheckOrderScopeAsync(RequestDraft draft, IReadOnlyDictionary<int, string> names, CancellationToken ct)
    {
        if (draft.OrderId is not int orderId)
            return SewingTeamScope.Of(_user) is null ? Result.Success() : Invalid(Messages.RequisitionOrderRequired);
        if (draft.WorkOrderId is not null) return Result.Success();

        var ids = names.Keys.ToList();
        var ofOrder = await _uow.Repository<OrderMaterial>().Query().AsNoTracking()
            .Where(m => m.OrderId == orderId && ids.Contains(m.MaterialId)).Select(m => m.MaterialId).ToListAsync(ct);
        var outside = ids.Except(ofOrder).Select(id => names[id]).ToList();
        return outside.Count == 0
            ? Result.Success()
            : Invalid(string.Format(Messages.RequisitionMaterialNotInOrder, string.Join(", ", outside)));
    }

    /// <summary>Lệnh của tổ, đang mở / đang cắt; mỗi NVL là vải hệ thống tính được cho lệnh; đã xin ở đơn khác + đơn này ≤ trần.</summary>
    private async Task<Result> CheckWorkOrderCapAsync(RequestDraft draft, int workOrderId, IReadOnlyDictionary<int, string> names,
        CancellationToken ct)
    {
        var wo = await _needs.LoadAsync(workOrderId, ct);
        if (wo is null || wo.CuttingTeamId != _user.TeamId || wo.Status is not (WorkOrderStatuses.Open or WorkOrderStatuses.Cutting)
            || (draft.OrderId is int orderId && orderId != wo.OrderId))
            return Invalid(Messages.RequisitionWorkOrderInvalid);

        var allowance = (await _needs.AllowanceAsync(wo, draft.RequestId, ct)).ToDictionary(a => a.MaterialId);
        var over = new List<string>();
        foreach (var line in draft.Lines)
        {
            var id = line.MaterialId!.Value;
            if (!allowance.TryGetValue(id, out var a) || a.CapQty is not { } cap)
                return Invalid(string.Format(Messages.RequisitionNotWorkOrderFabric, names[id]));
            if (a.RequestedQty + line.Quantity > cap)
                over.Add(string.Format(Messages.RequisitionOverCapLine, names[id], Qty(cap), Qty(a.RequestedQty), Qty(line.Quantity)));
        }
        return over.Count == 0 ? Result.Success() : Invalid(string.Format(Messages.RequisitionOverWorkOrderCap, string.Join("; ", over)));
    }
}

/// <summary>
/// Xin BTP (tổ may): bắt buộc chọn đơn hàng; mỗi dòng là một dòng đơn (mã hàng × màu × size, cả bộ) của đơn đó; số lượng bộ là số
/// nguyên từ 1 đến 1.000.000 và không vượt số bộ khả dụng trong kho BTP (DEV-ALL-110). Dòng chia cho tổ kiểm ở RequestService.
/// </summary>
public sealed class SemiFinishedRequisitionRequestHandler : RequisitionRequestHandler
{
    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;

    public SemiFinishedRequisitionRequestHandler(IClock clock, IUnitOfWork uow, IStockService stock) : base(clock)
    {
        _uow = uow; _stock = stock;
    }

    public override string RequestType => RequestTypes.SemiFinishedRequisition;

    protected override async Task<Result> CheckLinesAsync(RequestDraft draft, CancellationToken ct)
    {
        if (draft.Lines.Any(l => l.Quantity < 1 || l.Quantity > RequisitionLimits.MaxPieceQty || l.Quantity != decimal.Truncate(l.Quantity)))
            return Invalid(Messages.RequisitionPieceQtyInvalid);
        if (draft.OrderId is not int orderId) return Invalid(Messages.RequisitionOrderRequired);
        if (draft.Lines.Any(l => l.OrderLineId is null || l.ProductComponentMaterialId is not null))
            return Invalid(Messages.RequisitionPieceLineRequired);

        var ids = draft.Lines.Select(l => l.OrderLineId!.Value).ToList();
        var labels = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => ids.Contains(l.OrderLineId) && l.OrderId == orderId)
            .Select(l => new { l.OrderLineId, l.ProductVariant.Product.ProductCode, l.ProductVariant.ProductColor.Color, l.ProductVariant.SizeCode })
            .ToDictionaryAsync(l => l.OrderLineId, l => string.Format(Messages.OrderLineLabel, l.ProductCode, l.Color, l.SizeCode), ct);
        if (labels.Count != ids.Count) return Invalid(Messages.RequisitionOrderLineNotInOrder);

        var available = await _stock.GetSetAvailableAsync(ids, ct);
        return CheckStock(draft.Lines.Select(l => (labels[l.OrderLineId!.Value], l.Quantity, available.GetValueOrDefault(l.OrderLineId!.Value))));
    }
}
