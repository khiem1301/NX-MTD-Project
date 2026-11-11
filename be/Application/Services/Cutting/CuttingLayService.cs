using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC51: tổ trưởng cắt nhập bàn cắt theo lệnh cắt: số lượng sau cắt từng màu - size và số mét vải đã dùng của bàn (DEV-ALL-90).
/// Chặn khi vượt kế hoạch + % dư hoặc vượt vải còn lại của tổ; cảnh báo khi vượt kế hoạch.
/// </summary>
public sealed class CuttingLayService : ICuttingLayService
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IUnitOfWork _uow;
    private readonly IAppLock _locks;
    private readonly IClock _clock;
    private readonly FabricPlanner _planner;

    public CuttingLayService(IUnitOfWork uow, IAppLock locks, IClock clock, FabricPlanner planner)
    {
        _uow = uow; _locks = locks; _clock = clock; _planner = planner;
    }

    private IQueryable<CuttingLay> Lays => _uow.Repository<CuttingLay>().Query();

    public async Task<Result<PagedResult<CuttingLayDto>>> GetPagedAsync(CuttingLayQuery query, int cuttingTeamId,
        CancellationToken ct)
    {
        var page = await Lays.AsNoTracking()
            .Where(l => l.CuttingTeamId == cuttingTeamId)
            .Where(l => query.WorkOrderId == null || l.WorkOrderId == query.WorkOrderId)
            .Where(l => query.WorkDate == null || l.WorkDate == query.WorkDate)
            .Where(l => string.IsNullOrEmpty(query.Status) || l.Status == query.Status)
            .OrderByDescending(l => l.WorkDate).ThenByDescending(l => l.CuttingLayId)
            .Select(ToDto())
            .ToPagedAsync(query, ct);
        return Result<PagedResult<CuttingLayDto>>.Success(page with { Items = page.Items.Select(WithSizeSummary).ToList() });
    }

    public async Task<Result<CuttingLayDto>> GetByIdAsync(int layId, int cuttingTeamId, CancellationToken ct)
    {
        var dto = await Lays.AsNoTracking().Where(l => l.CuttingLayId == layId).Select(ToDto()).FirstOrDefaultAsync(ct);
        if (dto is null) return WarehouseFailures.NotFound<CuttingLayDto>();
        dto = WithSizeSummary(dto);
        var teamId = await Lays.Where(l => l.CuttingLayId == layId).Select(l => l.CuttingTeamId).FirstAsync(ct);
        return teamId == cuttingTeamId ? Result<CuttingLayDto>.Success(dto) : WarehouseFailures.TeamMismatch<CuttingLayDto>();
    }

    public async Task<Result<CuttingLayDto>> CreateAsync(SaveCuttingLayDto dto, int cuttingTeamId, CancellationToken ct)
    {
        var saved = await _uow.InTransactionAsync(async () =>
        {
            var (error, wo) = await LoadWorkOrderAsync(dto.WorkOrderId, cuttingTeamId, ct);
            if (error is not null) return WarehouseFailures.From<(int, string?)>(error);

            var lay = new CuttingLay { CuttingTeamId = cuttingTeamId, Status = CuttingLayStatuses.Recorded };
            var applied = await ApplyAsync(lay, dto, wo!, ct);
            if (!applied.IsSuccess) return WarehouseFailures.From<(int, string?)>(applied);
            lay.LaySequence = await NextSequenceAsync(cuttingTeamId, dto.WorkDate, dto.ShiftCode, ct);

            if (wo!.Status == WorkOrderStatuses.Open) wo.Status = WorkOrderStatuses.Cutting;
            await _uow.Repository<CuttingLay>().AddAsync(lay);
            await _uow.SaveChangesAsync();
            return Result<(int, string?)>.Success((lay.CuttingLayId, applied.Message));
        });
        return await ReloadAsync(saved, cuttingTeamId, ct);
    }

    public async Task<Result<CuttingLayDto>> UpdateAsync(int layId, SaveCuttingLayDto dto, int cuttingTeamId,
        CancellationToken ct)
    {
        var saved = await _uow.InTransactionAsync(async () =>
        {
            var lay = await Lays.Include(l => l.CuttingLayLines).FirstOrDefaultAsync(l => l.CuttingLayId == layId, ct);
            if (lay is null) return WarehouseFailures.NotFound<(int, string?)>();
            if (lay.CuttingTeamId != cuttingTeamId) return WarehouseFailures.TeamMismatch<(int, string?)>();
            if (lay.Status != CuttingLayStatuses.Recorded)
                return Result<(int, string?)>.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.LayLocked);

            var (error, wo) = await LoadWorkOrderAsync(dto.WorkOrderId, cuttingTeamId, ct);
            if (error is not null) return WarehouseFailures.From<(int, string?)>(error);

            var moved = lay.WorkDate != dto.WorkDate || lay.ShiftCode != dto.ShiftCode;
            _uow.Repository<CuttingLayLine>().RemoveRange(lay.CuttingLayLines.ToList());
            var applied = await ApplyAsync(lay, dto, wo!, ct);
            if (!applied.IsSuccess) return WarehouseFailures.From<(int, string?)>(applied);
            if (moved) lay.LaySequence = await NextSequenceAsync(cuttingTeamId, dto.WorkDate, dto.ShiftCode, ct);

            await _uow.SaveChangesAsync();
            return Result<(int, string?)>.Success((lay.CuttingLayId, applied.Message));
        });
        return await ReloadAsync(saved, cuttingTeamId, ct);
    }

    private async Task<Result<CuttingLayDto>> ReloadAsync(Result<(int Id, string? Warning)> saved, int teamId,
        CancellationToken ct)
    {
        if (!saved.IsSuccess) return WarehouseFailures.From<CuttingLayDto>(saved);
        var dto = await GetByIdAsync(saved.Data.Id, teamId, ct);
        return Result<CuttingLayDto>.Success(dto.Data!, saved.Data.Warning);
    }

    private async Task<(Result? Error, WorkOrder? WorkOrder)> LoadWorkOrderAsync(int workOrderId, int teamId,
        CancellationToken ct)
    {
        var wo = await _uow.Repository<WorkOrder>().Query().Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine).ThenInclude(o => o.ProductVariant).ThenInclude(v => v.ProductColor)
            .FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);
        if (wo is null) return (WarehouseFailures.NotFound(), null);
        if (wo.CuttingTeamId != teamId) return (WarehouseFailures.TeamMismatch(), null);
        return wo.Status is WorkOrderStatuses.Open or WorkOrderStatuses.Cutting
            ? (null, wo)
            : (Result.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.WorkOrderNotCuttable), null);
    }

    /// <summary>Gán dữ liệu bàn cắt và kiểm tra trần; Success kèm Message = cảnh báo vượt kế hoạch (nếu có).</summary>
    private async Task<Result> ApplyAsync(CuttingLay lay, SaveCuttingLayDto dto, WorkOrder wo, CancellationToken ct)
    {
        var planned = wo.WorkOrderLines.GroupBy(l => (l.OrderLineId, l.ProductComponentMaterialId))
            .ToDictionary(g => g.Key, g => g.Sum(l => l.PlannedQty));
        if (dto.Lines.Any(l => !planned.ContainsKey((l.OrderLineId, l.ProductComponentMaterialId))))
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.LayOrderLineNotInWorkOrder);
        var date = CheckWorkDate(dto.WorkDate, wo);
        if (date is not null) return date;
        var fabric = await CheckFabricAsync(dto, wo, ct) ?? await CheckFabricRemainingAsync(dto, wo, lay.CuttingLayId, ct);
        if (fabric is not null) return fabric;

        var lines = dto.Lines.Select(l => new CuttingLayLine
        {
            OrderLineId = l.OrderLineId,
            ProductComponentMaterialId = l.ProductComponentMaterialId,
            ActualQty = l.ActualQty,
        }).ToList();
        var cap = await CheckCapAsync(wo, lay.CuttingLayId, lines, planned, ct);
        if (!cap.IsSuccess) return cap;

        lay.WorkOrderId = wo.WorkOrderId;
        lay.WorkDate = dto.WorkDate;
        lay.ShiftCode = dto.ShiftCode;
        lay.FabricMaterialId = dto.FabricMaterialId;
        lay.FabricUsedM = dto.FabricUsedM;
        lay.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        lay.CuttingLayLines = lines;
        return cap;
    }

    /// <summary>Bàn cắt đã làm rồi mới ghi: ngày cắt không sau hôm nay, không trước ngày lập lệnh cắt.</summary>
    private Result? CheckWorkDate(DateOnly workDate, WorkOrder wo)
    {
        if (workDate > _clock.Today)
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.LayWorkDateFuture);
        var created = DateOnly.FromDateTime(wo.CreatedAt);
        return workDate < created
            ? Result.Failure(ErrorCodes.WarehouseInvalidReference,
                string.Format(WarehouseMessages.LayWorkDateBeforeWorkOrder, created.ToString("dd/MM/yyyy")))
            : null;
    }

    /// <summary>Vải của bàn phải là vải chi tiết PANEL trong định mức của các dòng đơn trên bàn; bàn cắt bù chi tiết phải đúng vải của dòng định mức.</summary>
    private async Task<Result?> CheckFabricAsync(SaveCuttingLayDto dto, WorkOrder wo, CancellationToken ct)
    {
        // Vải của bàn (đã gồm màu, 26/10) phải là vải chi tiết PANEL trong định mức của các dòng đơn trên bàn.
        var colorIds = wo.WorkOrderLines.Where(l => dto.Lines.Any(d => d.OrderLineId == l.OrderLineId))
            .Select(l => l.OrderLine.ProductVariant.ProductColorId).Distinct().ToList();
        if (!await _uow.Repository<ProductComponentMaterial>().Query()
                .AnyAsync(p => colorIds.Contains(p.ProductColorId) && p.MaterialId == dto.FabricMaterialId
                               && p.Material.MaterialType == MaterialTypes.Fabric
                               && p.ProductComponent.ComponentType == ComponentTypes.Panel, ct))
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.MaterialNotInWorkOrder);

        // Bàn cắt bù chi tiết phải cắt đúng vải của dòng định mức (database chặn cứng, THROW 50049).
        var normIds = dto.Lines.Where(l => l.ProductComponentMaterialId is not null)
            .Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToList();
        if (normIds.Count > 0)
        {
            var matching = await _uow.Repository<ProductComponentMaterial>().Query()
                .CountAsync(p => normIds.Contains(p.ProductComponentMaterialId) && p.MaterialId == dto.FabricMaterialId, ct);
            if (matching != normIds.Count)
                return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.LayComponentFabricMismatch);
        }
        return null;
    }

    /// <summary>
    /// Vải còn lại của tổ trên đơn = vải tổ đang giữ (đã nhận − đã trả về kho) − số mét các bàn khác của tổ trên đơn đã dùng,
    /// cùng cách tính với "vải còn lại" ở lệnh cắt. Bàn này dùng nhiều hơn số còn lại thì chặn. Bàn cũ chưa có số mét không trừ được.
    /// </summary>
    private async Task<Result?> CheckFabricRemainingAsync(SaveCuttingLayDto dto, WorkOrder wo, int exceptLayId, CancellationToken ct)
    {
        var held = Math.Max(0, await _planner.GetTeamHoldingAsync(wo.CuttingTeamId, wo.OrderId, dto.FabricMaterialId, ct));
        var usedByOthers = await Lays
            .Where(l => l.CuttingTeamId == wo.CuttingTeamId && l.WorkOrder.OrderId == wo.OrderId
                        && l.FabricMaterialId == dto.FabricMaterialId && l.CuttingLayId != exceptLayId)
            .SumAsync(l => l.FabricUsedM ?? 0m, ct);
        var remaining = Math.Max(0, held - usedByOthers);
        var used = dto.FabricUsedM ?? 0m;
        return used <= remaining
            ? null
            : Result.Failure(ErrorCodes.WarehouseInsufficientStock,
                string.Format(WarehouseMessages.LayFabricOverRemaining, Format(used), Format(remaining)));
    }

    private static string Format(decimal qty) => qty.ToString("#,0.##", Vietnamese);

    private async Task<Result> CheckCapAsync(WorkOrder wo, int exceptLayId, IReadOnlyList<CuttingLayLine> lines,
        IReadOnlyDictionary<(int OrderLineId, int? ProductComponentMaterialId), int> planned, CancellationToken ct)
    {
        var surplus = await SurplusPercentAsync(ct);
        var cut = await _uow.Repository<CuttingLayLine>().Query()
            .Where(l => l.CuttingLay.WorkOrderId == wo.WorkOrderId && l.CuttingLayId != exceptLayId)
            .GroupBy(l => new { l.OrderLineId, l.ProductComponentMaterialId }).Select(g => new { g.Key, Qty = g.Sum(l => l.ActualQty) })
            .ToDictionaryAsync(x => (x.Key.OrderLineId, x.Key.ProductComponentMaterialId), x => x.Qty, ct);

        var over = new List<string>();
        var warn = false;
        foreach (var line in lines)
        {
            var ol = wo.WorkOrderLines.First(l => l.OrderLineId == line.OrderLineId).OrderLine;
            var plan = planned[(line.OrderLineId, line.ProductComponentMaterialId)];
            var before = cut.GetValueOrDefault((line.OrderLineId, line.ProductComponentMaterialId));
            switch (CuttingLayCalculator.Check(plan, before, line.ActualQty, surplus))
            {
                case CutCapCheck.OverCap:
                    over.Add($"{ol.ProductVariant.ProductColor.Color}-{ol.ProductVariant.SizeCode}: kế hoạch {plan}, tối đa {CuttingLayCalculator.Cap(plan, surplus)}, đã cắt {before}, bàn này {line.ActualQty}");
                    break;
                case CutCapCheck.OverPlanWithinSurplus:
                    warn = true;
                    break;
            }
        }
        if (over.Count > 0)
            return Result.Failure(ErrorCodes.CuttingQtyExceedsPlan, $"{WarehouseMessages.LayQtyExceedsPlan} ({surplus}%): {string.Join("; ", over)}.");
        return Result.Success(warn ? WarehouseMessages.LayQtyOverPlanWarning : null);
    }

    private async Task<decimal> SurplusPercentAsync(CancellationToken ct)
    {
        var raw = await _uow.Repository<SystemSetting>().Query().Where(s => s.SettingKey == SettingKeys.CutSurplusPercent)
            .Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    /// <summary>Số thứ tự bàn trong ca của tổ (unique theo tổ + ngày + ca); khóa để hai người lưu cùng lúc không trùng.</summary>
    private async Task<short> NextSequenceAsync(int teamId, DateOnly date, string shift, CancellationToken ct)
    {
        await _locks.AcquireAsync($"lay-seq:{teamId}:{date:yyyyMMdd}:{shift}", ct);
        var max = await Lays.Where(l => l.CuttingTeamId == teamId && l.WorkDate == date && l.ShiftCode == shift)
            .MaxAsync(l => (short?)l.LaySequence, ct);
        return (short)((max ?? 0) + 1);
    }

    /// <summary>Số lượng sau cắt theo size, VD "S:20, M:30"; lệnh nhiều màu thì ghi kèm màu "Đỏ-S:20".</summary>
    private static CuttingLayDto WithSizeSummary(CuttingLayDto lay)
    {
        var multiColor = lay.Lines.Select(l => l.Color).Distinct().Count() > 1;
        return lay with
        {
            SizeSummary = string.Join(", ", lay.Lines.Select(l => $"{(multiColor ? l.Color + "-" : string.Empty)}{l.SizeCode}:{l.ActualQty}")),
        };
    }

    private static System.Linq.Expressions.Expression<Func<CuttingLay, CuttingLayDto>> ToDto() => l =>
        new CuttingLayDto(l.CuttingLayId, l.WorkOrderId, l.WorkOrder.WorkOrderCode, l.WorkDate, l.ShiftCode,
            l.LaySequence, l.FabricMaterialId, l.FabricMaterial.MaterialCode,
            l.FabricMaterial.Color, l.FabricUsedM, string.Empty, l.Note, l.Status, l.CreatedAt,
            l.CuttingLayLines.OrderBy(x => x.CuttingLayLineId).Select(x => new CuttingLayLineDto(x.CuttingLayLineId,
                x.OrderLineId, x.ProductComponentMaterialId, x.ProductComponentMaterial == null ? null : x.ProductComponentMaterial.ProductComponent.ComponentName + " – " + x.ProductComponentMaterial.Material.MaterialName + (x.ProductComponentMaterial.Material.Color == "" ? "" : " " + x.ProductComponentMaterial.Material.Color),
                x.OrderLine.ProductVariant.ProductColor.Color, x.OrderLine.ProductVariant.SizeCode, x.ActualQty)).ToList());
}
