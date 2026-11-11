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

/// <summary>Kiểm chung của đơn Xin NVL / Xin BTP của tổ trưởng: có dòng, tối đa 50 dòng, không trùng, ngày cần không ở quá khứ.</summary>
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
}

/// <summary>
/// Xin NVL (tổ cắt, tổ may): mỗi dòng phải chọn NVL trong danh mục đang dùng. Vải: số lượng lớn hơn 0, tối đa 2 chữ số sau dấu phẩy;
/// NVL khác vải (chỉ, cúc, phụ liệu…): số nguyên. Tối đa 9.999.999 mỗi dòng.
/// Có chọn lệnh cắt (tổ cắt, DEV-ALL-92): chỉ xin vải hệ thống tính cho lệnh, tổng các đơn xin NVL của lệnh không vượt trần của lệnh.
/// </summary>
public sealed class MaterialRequisitionRequestHandler : RequisitionRequestHandler
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IWorkOrderFabricNeeds _needs;

    public MaterialRequisitionRequestHandler(IClock clock, IUnitOfWork uow, ICurrentUser user, IWorkOrderFabricNeeds needs) : base(clock)
    {
        _uow = uow; _user = user; _needs = needs;
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
            .Where(m => ids.Contains(m.MaterialId) && m.Status == MaterialStatuses.Active)
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
        return draft.WorkOrderId is int workOrderId
            ? await CheckWorkOrderCapAsync(draft, workOrderId, materials.ToDictionary(m => m.Key, m => m.Value.MaterialName), ct)
            : Result.Success();
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

    private static string Qty(decimal value) => value.ToString("#,0.##", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));
}

/// <summary>Xin BTP (tổ may): số lượng bộ là số nguyên từ 1 đến 1.000.000.</summary>
public sealed class SemiFinishedRequisitionRequestHandler : RequisitionRequestHandler
{
    public SemiFinishedRequisitionRequestHandler(IClock clock) : base(clock) { }

    public override string RequestType => RequestTypes.SemiFinishedRequisition;

    protected override Task<Result> CheckLinesAsync(RequestDraft draft, CancellationToken ct) =>
        Task.FromResult(draft.Lines.Any(l => l.Quantity < 1 || l.Quantity > RequisitionLimits.MaxPieceQty || l.Quantity != decimal.Truncate(l.Quantity))
            ? Invalid(Messages.RequisitionPieceQtyInvalid)
            : Result.Success());
}
