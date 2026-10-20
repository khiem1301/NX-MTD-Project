using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>UC51: tổ trưởng cắt nhập bàn cắt theo lệnh cắt; chặn khi vượt kế hoạch + % dư, cảnh báo khi vượt kế hoạch.</summary>
public sealed class CuttingLayService : ICuttingLayService
{
    private readonly IUnitOfWork _uow;
    private readonly IAppLock _locks;

    public CuttingLayService(IUnitOfWork uow, IAppLock locks)
    {
        _uow = uow; _locks = locks;
    }

    private IQueryable<CuttingLay> Lays => _uow.Repository<CuttingLay>().Query();

    public async Task<Result<PagedResult<CuttingLayDto>>> GetPagedAsync(CuttingLayQuery query, int cuttingTeamId,
        CancellationToken ct)
    {
        var page = await Lays.AsNoTracking()
            .Where(l => l.CuttingTeamId == cuttingTeamId)
            .Where(l => query.WorkOrderId == null || l.WorkOrderId == query.WorkOrderId)
            .Where(l => query.WorkDate == null || l.WorkDate == query.WorkDate)
            .OrderByDescending(l => l.WorkDate).ThenByDescending(l => l.CuttingLayId)
            .Select(ToDto())
            .ToPagedAsync(query, ct);
        return Result<PagedResult<CuttingLayDto>>.Success(page);
    }

    public async Task<Result<CuttingLayDto>> GetByIdAsync(int layId, int cuttingTeamId, CancellationToken ct)
    {
        var dto = await Lays.AsNoTracking().Where(l => l.CuttingLayId == layId).Select(ToDto()).FirstOrDefaultAsync(ct);
        if (dto is null) return WarehouseFailures.NotFound<CuttingLayDto>();
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
        var wo = await _uow.Repository<WorkOrder>().Query().Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine)
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
        var planned = wo.WorkOrderLines.GroupBy(l => l.OrderLineId).ToDictionary(g => g.Key, g => g.Sum(l => l.PlannedQty));
        if (dto.Lines.Any(l => !planned.ContainsKey(l.OrderLineId)))
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.LayOrderLineNotInWorkOrder);
        if (dto.FabricMaterialId is not null && !await _uow.Repository<Material>().Query()
                .AnyAsync(m => m.MaterialId == dto.FabricMaterialId && m.MaterialType == MaterialTypes.Fabric, ct))
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.MaterialNotInWorkOrder);

        var lines = dto.Lines.Select(l =>
        {
            var calculated = CuttingLayCalculator.CalculatedQty(dto.PlyCount, l.RatioValue);
            var actual = l.ActualQty ?? calculated;
            return new CuttingLayLine
            {
                OrderLineId = l.OrderLineId, RatioValue = (short)l.RatioValue, CalculatedQty = calculated,
                ActualQty = actual, AdjustReason = actual == calculated ? null : l.AdjustReason?.Trim(),
            };
        }).ToList();
        if (lines.Any(l => l.ActualQty != l.CalculatedQty && string.IsNullOrEmpty(l.AdjustReason)))
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ReasonRequired);

        var cap = await CheckCapAsync(wo, lay.CuttingLayId, lines, planned, ct);
        if (!cap.IsSuccess) return cap;

        lay.WorkOrderId = wo.WorkOrderId;
        lay.WorkDate = dto.WorkDate;
        lay.ShiftCode = dto.ShiftCode;
        lay.FabricMaterialId = dto.FabricMaterialId;
        lay.FabricColor = dto.FabricColor.Trim();
        lay.PlyCount = (short)dto.PlyCount;
        lay.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        lay.SizeRatio = SizeRatioText(wo, lines);
        lay.CuttingLayLines = lines;
        return cap;
    }

    private async Task<Result> CheckCapAsync(WorkOrder wo, int exceptLayId, IReadOnlyList<CuttingLayLine> lines,
        IReadOnlyDictionary<int, int> planned, CancellationToken ct)
    {
        var surplus = await SurplusPercentAsync(ct);
        var cut = await _uow.Repository<CuttingLayLine>().Query()
            .Where(l => l.CuttingLay.WorkOrderId == wo.WorkOrderId && l.CuttingLayId != exceptLayId)
            .GroupBy(l => l.OrderLineId).Select(g => new { g.Key, Qty = g.Sum(l => l.ActualQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        var over = new List<string>();
        var warn = false;
        foreach (var line in lines)
        {
            var ol = wo.WorkOrderLines.First(l => l.OrderLineId == line.OrderLineId).OrderLine;
            var plan = planned[line.OrderLineId];
            var before = cut.GetValueOrDefault(line.OrderLineId);
            switch (CuttingLayCalculator.Check(plan, before, line.ActualQty, surplus))
            {
                case CutCapCheck.OverCap:
                    over.Add($"{ol.Color}-{ol.SizeCode}: kế hoạch {plan}, tối đa {CuttingLayCalculator.Cap(plan, surplus)}, đã cắt {before}, bàn này {line.ActualQty}");
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

    /// <summary>VD "S:2, M:3, L:2"; lệnh nhiều màu thì ghi kèm màu "Đỏ-S:2".</summary>
    private static string SizeRatioText(WorkOrder wo, IEnumerable<CuttingLayLine> lines)
    {
        var orderLines = wo.WorkOrderLines.Select(l => l.OrderLine).DistinctBy(l => l.OrderLineId).ToDictionary(l => l.OrderLineId);
        var multiColor = lines.Select(l => orderLines[l.OrderLineId].Color).Distinct().Count() > 1;
        return string.Join(", ", lines.Select(l =>
        {
            var ol = orderLines[l.OrderLineId];
            return $"{(multiColor ? ol.Color + "-" : string.Empty)}{ol.SizeCode}:{l.RatioValue}";
        }));
    }

    private static System.Linq.Expressions.Expression<Func<CuttingLay, CuttingLayDto>> ToDto() => l =>
        new CuttingLayDto(l.CuttingLayId, l.WorkOrderId, l.WorkOrder.WorkOrderCode, l.WorkDate, l.ShiftCode,
            l.LaySequence, l.FabricMaterialId, l.FabricMaterial == null ? null : l.FabricMaterial.MaterialCode,
            l.FabricColor, l.PlyCount, l.SizeRatio, l.Note, l.Status, l.CreatedAt,
            l.CuttingLayLines.OrderBy(x => x.CuttingLayLineId).Select(x => new CuttingLayLineDto(x.CuttingLayLineId,
                x.OrderLineId, x.OrderLine.Color, x.OrderLine.SizeCode, x.RatioValue, x.CalculatedQty, x.ActualQty,
                x.AdjustReason)).ToList());
}
