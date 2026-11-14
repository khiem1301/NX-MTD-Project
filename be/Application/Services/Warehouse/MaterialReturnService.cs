using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC42 (TH-10, DEV-ALL-112): tổ cắt và tổ may trả NVL / phế phẩm / BTP về kho. Tổ cắt trả NVL theo lệnh cắt; tổ may trả NVL không
/// theo đơn và BTP theo đơn – mã hàng – màu – size (cả bộ). Cả hai tổ chọn thêm NVL phế phẩm (không có trần). Trần số trả tính ở
/// <see cref="MaterialReturnCapacity"/>. Thủ kho đếm, xác nhận (cộng kho, lệch phải có lý do) hoặc từ chối có lý do.
/// </summary>
public sealed class MaterialReturnService : IMaterialReturnService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly IDocumentCodeGenerator _codes;
    private readonly INotificationService _notifier;
    private readonly IAppLock _locks;
    private readonly IClock _clock;
    private readonly MaterialReturnCapacity _capacity;

    public MaterialReturnService(IUnitOfWork uow, IStockService stock, IDocumentCodeGenerator codes,
        INotificationService notifier, IAppLock locks, IClock clock, MaterialReturnCapacity capacity)
    {
        _uow = uow; _stock = stock; _codes = codes; _notifier = notifier; _locks = locks; _clock = clock; _capacity = capacity;
    }

    private IQueryable<MaterialReturn> Returns => _uow.Repository<MaterialReturn>().Query();

    public async Task<Result<PagedResult<MaterialReturnListItemDto>>> GetPagedAsync(MaterialReturnQuery query, int? teamId,
        CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Returns.AsNoTracking()
            .Where(r => teamId == null || r.ReturningTeamId == teamId)
            .Where(r => string.IsNullOrEmpty(query.Status) || r.Status == query.Status)
            .Where(r => string.IsNullOrEmpty(keyword) || r.ReturnCode.Contains(keyword)
                        || (r.Order != null && r.Order.OrderCode.Contains(keyword))
                        || (r.WorkOrder != null && r.WorkOrder.WorkOrderCode.Contains(keyword)))
            .OrderByDescending(r => r.MaterialReturnId)
            .Select(r => new MaterialReturnListItemDto(r.MaterialReturnId, r.ReturnCode, r.WarehouseType, r.ReturningTeamId,
                r.ReturningTeam.TeamName, r.Order == null ? null : r.Order.OrderCode,
                r.WorkOrder == null ? null : r.WorkOrder.WorkOrderCode, r.Status, r.CreatedAt,
                r.ConfirmedByNavigation == null ? null : r.ConfirmedByNavigation.FullName, r.ConfirmedAt,
                r.MaterialReturnLines.Sum(l => l.ReturnedQty)))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<MaterialReturnListItemDto>>.Success(page);
    }

    public async Task<Result<MaterialReturnDto>> GetByIdAsync(int returnId, int? teamId, CancellationToken ct)
    {
        var dto = await Returns.AsNoTracking().Where(r => r.MaterialReturnId == returnId)
            .Select(r => new MaterialReturnDto(r.MaterialReturnId, r.ReturnCode, r.WarehouseType, r.ReturningTeamId,
                r.ReturningTeam.TeamName, r.Order == null ? null : r.Order.OrderCode,
                r.WorkOrder == null ? null : r.WorkOrder.WorkOrderCode, r.Note, r.Status, r.CreatedAt,
                r.ConfirmedByNavigation == null ? null : r.ConfirmedByNavigation.FullName, r.ConfirmedAt, r.RejectReason,
                r.MaterialReturnLines.OrderBy(l => l.MaterialReturnLineId).Select(l => new MaterialReturnLineDto(
                    l.MaterialReturnLineId, l.MaterialId, l.OrderLineId,
                    l.Material != null ? l.Material.MaterialCode : l.OrderLine!.ProductVariant.Product.ProductCode,
                    l.Material != null ? l.Material.MaterialName
                        : l.OrderLine!.ProductVariant.ProductColor.Color + "/" + l.OrderLine.ProductVariant.SizeCode,
                    l.Material != null ? l.Material.Unit : MaterialReturnCapacity.PieceUnit,
                    l.Material != null ? l.Material.Color : l.OrderLine!.ProductVariant.ProductColor.Color,
                    l.Material != null && l.Material.MaterialType == MaterialTypes.Scrap,
                    l.ReturnedQty, l.ConfirmedQty, l.DiscrepancyReason)).ToList()))
            .FirstOrDefaultAsync(ct);
        if (dto is null) return WarehouseFailures.NotFound<MaterialReturnDto>();
        return teamId is not null && dto.TeamId != teamId
            ? WarehouseFailures.TeamMismatch<MaterialReturnDto>()
            : Result<MaterialReturnDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyList<ReturnableLineDto>>> GetReturnableAsync(int teamId, ReturnableQuery query,
        CancellationToken ct)
    {
        var team = await LoadTeamAsync(teamId, ct);
        var scope = await CheckScopeAsync(team, query.WarehouseType, query.WorkOrderId, query.OrderId, ct);
        if (scope.Error is not null) return WarehouseFailures.From<IReadOnlyList<ReturnableLineDto>>(scope.Error);
        var lines = await ReturnableOfAsync(team!, query.WarehouseType, scope.WorkOrderId, query.OrderId, ct);
        return Result<IReadOnlyList<ReturnableLineDto>>.Success(lines);
    }

    private Task<IReadOnlyList<ReturnableLineDto>> ReturnableOfAsync(Team team, string warehouseType, int? workOrderId, int? orderId,
        CancellationToken ct) =>
        warehouseType == WarehouseTypes.SemiFinished ? _capacity.SemiFinishedAsync(team.TeamId, orderId!.Value, ct)
        : team.TeamType == TeamTypes.Cutting
            ? workOrderId is null ? Task.FromResult<IReadOnlyList<ReturnableLineDto>>([]) : _capacity.CuttingMaterialsAsync(team.TeamId, workOrderId.Value, ct)
            : _capacity.SewingMaterialsAsync(team.TeamId, ct);

    private Task<Team?> LoadTeamAsync(int teamId, CancellationToken ct) =>
        _uow.Repository<Team>().Query().AsNoTracking().FirstOrDefaultAsync(t => t.TeamId == teamId, ct)!;

    private sealed record Scope(Result? Error, int? WorkOrderId, int? OrderId);

    /// <summary>
    /// Tổ cắt chỉ trả NVL / phế phẩm (NVL theo lệnh cắt của tổ); tổ may trả NVL (không theo đơn) hoặc BTP (bắt buộc đơn hàng).
    /// Trả về lệnh cắt và đơn hàng ghi vào phiếu.
    /// </summary>
    private async Task<Scope> CheckScopeAsync(Team? team, string warehouseType, int? workOrderId, int? orderId, CancellationToken ct)
    {
        if (team is null || team.TeamType is not (TeamTypes.Cutting or TeamTypes.Sewing))
            return new Scope(WarehouseFailures.TeamMismatch(), null, null);
        if (warehouseType == WarehouseTypes.SemiFinished)
        {
            if (team.TeamType != TeamTypes.Sewing) return Rejected(WarehouseMessages.ReturnWarehouseNotAllowed);
            return orderId is null ? Rejected(WarehouseMessages.ReturnOrderRequired) : new Scope(null, null, orderId);
        }
        if (warehouseType != WarehouseTypes.Material) return Rejected(WarehouseMessages.ReturnWarehouseNotAllowed);
        if (team.TeamType == TeamTypes.Sewing || workOrderId is null) return new Scope(null, null, null);

        var wo = await _uow.Repository<WorkOrder>().Query().AsNoTracking().FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);
        return wo is null || wo.CuttingTeamId != team.TeamId || wo.Status == WorkOrderStatuses.Cancelled
            ? Rejected(WarehouseMessages.ReturnWorkOrderInvalid)
            : new Scope(null, wo.WorkOrderId, wo.OrderId);
    }

    private static Scope Rejected(string message) =>
        new(Result.Failure(ErrorCodes.WarehouseInvalidReference, message), null, null);

    public async Task<Result<MaterialReturnDto>> CreateAsync(CreateMaterialReturnDto dto, int teamId, CancellationToken ct)
    {
        var created = await _uow.InTransactionAsync(() => StoreReturnAsync(dto, teamId, ct));
        return created.IsSuccess ? await GetByIdAsync(created.Data, teamId, ct) : WarehouseFailures.From<MaterialReturnDto>(created);
    }

    /// <summary>Kiểm tổ, phạm vi trả và từng dòng (trần số trả) rồi lưu phiếu trả và báo Thủ kho.</summary>
    private async Task<Result<int>> StoreReturnAsync(CreateMaterialReturnDto dto, int teamId, CancellationToken ct)
    {
        var team = await LoadTeamAsync(teamId, ct);
        var scope = await CheckScopeAsync(team, dto.WarehouseType, dto.WorkOrderId, dto.OrderId, ct);
        if (scope.Error is not null) return WarehouseFailures.From<int>(scope.Error);

        // Khóa theo tổ để hai phiếu trả cùng lúc không cùng thấy "còn trả được" (phiếu chờ tính vào số đã trả).
        await _locks.AcquireAsync($"material-return:{teamId}", ct);
        var lines = await CheckLinesAsync(dto, team!, scope, ct);
        if (lines is not null) return WarehouseFailures.From<int>(lines);

        var ret = new MaterialReturn
        {
            ReturnCode = await _codes.NextAsync<MaterialReturn>(DocumentPrefixes.MaterialReturn, r => r.ReturnCode, ct),
            ReturningTeamId = teamId,
            WarehouseType = dto.WarehouseType,
            OrderId = scope.OrderId,
            WorkOrderId = scope.WorkOrderId,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
            Status = HandoverStatuses.Pending,
            CreatedAt = _clock.Now,
            MaterialReturnLines = dto.Lines.Select(l => new MaterialReturnLine
            {
                MaterialId = l.MaterialId,
                OrderLineId = l.OrderLineId,
                ReturnedQty = l.ReturnedQty,
            }).ToList(),
        };
        await _uow.Repository<MaterialReturn>().AddAsync(ret);
        await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
            "Chờ nhận hàng trả", $"Tổ {team!.TeamName} trả hàng về kho (phiếu chờ xác nhận), vui lòng đếm và nhận vào kho.", null), ct);
        await _uow.SaveChangesAsync();
        return Result<int>.Success(ret.MaterialReturnId, WarehouseMessages.ReturnCreated);
    }

    private async Task<Result?> CheckLinesAsync(CreateMaterialReturnDto dto, Team team, Scope scope, CancellationToken ct)
    {
        if (dto.WarehouseType == WarehouseTypes.SemiFinished)
            return CheckCap(dto.Lines.Select(l => (l.OrderLineId!.Value, l.ReturnedQty)),
                await _capacity.SemiFinishedAsync(team.TeamId, scope.OrderId!.Value, ct), byOrderLine: true);

        var ids = dto.Lines.Select(l => l.MaterialId!.Value).ToList();
        var materials = await _uow.Repository<Material>().Query().AsNoTracking()
            .Where(m => ids.Contains(m.MaterialId) && m.Status == MaterialStatuses.Active).ToDictionaryAsync(m => m.MaterialId, ct);
        var invalid = ids.FirstOrDefault(id => !materials.ContainsKey(id));
        if (invalid != 0) return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.IssueMaterialInvalid);
        var notWhole = dto.Lines.Where(l => NeedsWholeQty(materials[l.MaterialId!.Value]) && l.ReturnedQty != decimal.Truncate(l.ReturnedQty))
            .Select(l => materials[l.MaterialId!.Value].MaterialName).ToList();
        if (notWhole.Count > 0)
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, string.Format(WarehouseMessages.ReturnWholeQtyRequired, string.Join(", ", notWhole)));

        var capped = dto.Lines.Where(l => materials[l.MaterialId!.Value].MaterialType != MaterialTypes.Scrap).ToList();
        if (capped.Count == 0) return null;
        if (team.TeamType == TeamTypes.Cutting && scope.WorkOrderId is null)
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ReturnWorkOrderRequired);
        var available = await ReturnableOfAsync(team, dto.WarehouseType, scope.WorkOrderId, scope.OrderId, ct);
        return CheckCap(capped.Select(l => (l.MaterialId!.Value, l.ReturnedQty)), available, byOrderLine: false);
    }

    /// <summary>NVL không phải vải, và phế phẩm tính theo đơn vị khác kg (cái…), chỉ trả số nguyên.</summary>
    private static bool NeedsWholeQty(Material m) => m.MaterialType switch
    {
        MaterialTypes.Fabric => false,
        MaterialTypes.Scrap => !string.Equals(m.Unit, MaterialTypes.ScrapDecimalUnit, StringComparison.OrdinalIgnoreCase),
        _ => true,
    };

    /// <summary>Mỗi dòng phải có trong danh sách còn trả được và không vượt trần; liệt kê mọi dòng vượt trong một lỗi.</summary>
    private static Result? CheckCap(IEnumerable<(int Id, decimal Qty)> asked, IReadOnlyList<ReturnableLineDto> available, bool byOrderLine)
    {
        var cap = available.ToDictionary(a => (byOrderLine ? a.OrderLineId : a.MaterialId)!.Value);
        var over = new List<string>();
        foreach (var (id, qty) in asked)
        {
            if (!cap.TryGetValue(id, out var line))
                return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ReturnItemNotReturnable);
            if (qty > line.RemainingQty)
                over.Add($"{line.ItemCode} {line.ItemName}: còn trả được {line.RemainingQty:0.###}, trả {qty:0.###}");
        }
        return over.Count == 0
            ? null
            : Result.Failure(ErrorCodes.ReturnExceedsIssued, $"{WarehouseMessages.ReturnExceedsIssued}: {string.Join("; ", over)}.");
    }

    public Task<Result> ConfirmAsync(int returnId, ConfirmMaterialReturnDto dto, int employeeId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var ret = await Returns.Include(r => r.MaterialReturnLines).FirstOrDefaultAsync(r => r.MaterialReturnId == returnId, ct);
            if (ret is null) return WarehouseFailures.NotFound();
            if (ret.Status != HandoverStatuses.Pending) return WarehouseFailures.InvalidStatus();

            var counted = ApplyCount(ret, dto);
            if (counted is not null) return counted;

            var today = _clock.Today;
            var movements = ret.MaterialReturnLines.Where(l => l.ConfirmedQty > 0)
                .Select(l => new StockMovement(StockKeyOf(ret.WarehouseType, l), l.ConfirmedQty!.Value,
                    StockDocTypes.MaterialReturn, ret.MaterialReturnId, l.MaterialReturnLineId, today)).ToList();
            if (movements.Count > 0)
            {
                var posted = await _stock.PostAsync(movements, ct);
                if (!posted.IsSuccess) return posted;
            }

            ret.Status = HandoverStatuses.Confirmed;
            ret.ConfirmedBy = employeeId;
            ret.ConfirmedAt = _clock.Now;
            await _notifier.NotifyTeamLeadersAsync(ret.ReturningTeamId, new NotificationMessage(NotificationSources.Warehouse,
                "Kho đã nhận hàng trả", $"Thủ kho đã xác nhận phiếu trả {ret.ReturnCode}.", "/warehouse/material-returns"), ct);
            await _uow.SaveChangesAsync();
            return Result.Success(WarehouseMessages.ReturnConfirmed);
        });

    private static StockKey StockKeyOf(string warehouseType, MaterialReturnLine line) =>
        warehouseType == WarehouseTypes.SemiFinished
            ? StockKey.SemiFinished(line.OrderLineId!.Value, null)
            : StockKey.Material(line.MaterialId!.Value);

    /// <summary>Ghi số Thủ kho đếm lên từng dòng: đủ mọi dòng, không vượt số tổ trả, lệch thì phải có lý do.</summary>
    private static Result? ApplyCount(MaterialReturn ret, ConfirmMaterialReturnDto dto)
    {
        var byId = dto.Lines.ToDictionary(l => l.MaterialReturnLineId);
        if (byId.Count != ret.MaterialReturnLines.Count || ret.MaterialReturnLines.Any(l => !byId.ContainsKey(l.MaterialReturnLineId)))
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.LinesRequired);
        foreach (var line in ret.MaterialReturnLines)
        {
            var input = byId[line.MaterialReturnLineId];
            if (input.ConfirmedQty > line.ReturnedQty)
                return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.HandoverConfirmedExceedsHanded);
            if (input.ConfirmedQty != line.ReturnedQty && string.IsNullOrWhiteSpace(input.DiscrepancyReason))
                return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DiscrepancyReasonRequired);
            line.ConfirmedQty = input.ConfirmedQty;
            line.DiscrepancyReason = input.ConfirmedQty == line.ReturnedQty ? null : input.DiscrepancyReason!.Trim();
        }
        return null;
    }

    public async Task<Result> RejectAsync(int returnId, ReasonDto dto, int employeeId, CancellationToken ct)
    {
        var ret = await Returns.FirstOrDefaultAsync(r => r.MaterialReturnId == returnId, ct);
        if (ret is null) return WarehouseFailures.NotFound();
        if (ret.Status != HandoverStatuses.Pending) return WarehouseFailures.InvalidStatus();

        ret.Status = HandoverStatuses.Rejected;
        ret.RejectReason = dto.Reason.Trim();
        ret.ConfirmedBy = employeeId;
        ret.ConfirmedAt = _clock.Now;
        await _notifier.NotifyTeamLeadersAsync(ret.ReturningTeamId, new NotificationMessage(NotificationSources.Warehouse,
            "Phiếu trả bị từ chối", $"Thủ kho từ chối phiếu trả {ret.ReturnCode}: {ret.RejectReason}", "/warehouse/material-returns"), ct);
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.ReturnRejected);
    }

    public async Task<Result> CancelAsync(int returnId, int teamId, CancellationToken ct)
    {
        var ret = await Returns.FirstOrDefaultAsync(r => r.MaterialReturnId == returnId && r.ReturningTeamId == teamId, ct);
        if (ret is null) return WarehouseFailures.NotFound();
        if (ret.Status != HandoverStatuses.Pending) return WarehouseFailures.InvalidStatus();

        ret.Status = HandoverStatuses.Cancelled;
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.ReturnCancelled);
    }
}
