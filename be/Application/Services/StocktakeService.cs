using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC48 / UC49 (TA-02): Thủ kho lập phiếu kiểm kê (số sổ = tồn thực chụp lúc lập, số đếm có thể để trống = khớp sổ);
/// Quản đốc / Admin duyệt → mỗi dòng lệch được ghi điều chỉnh vào thẻ kho qua <see cref="IStockService"/> (không âm),
/// hoặc trả lại có lý do để Thủ kho sửa và gửi lại.
/// </summary>
public sealed class StocktakeService : IStocktakeService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly IStockRepository _stockRepo;
    private readonly IDocumentCodeGenerator _codes;
    private readonly INotificationService _notifier;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;

    public StocktakeService(IUnitOfWork uow, IStockService stock, IStockRepository stockRepo, IDocumentCodeGenerator codes,
        INotificationService notifier, IAuditLogService audit, IClock clock)
    {
        _uow = uow; _stock = stock; _stockRepo = stockRepo; _codes = codes; _notifier = notifier; _audit = audit; _clock = clock;
    }

    private IQueryable<Stocktake> Stocktakes => _uow.Repository<Stocktake>().Query();

    public async Task<Result<PagedResult<StocktakeListItemDto>>> GetPagedAsync(StocktakeQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Stocktakes.AsNoTracking()
            .Where(s => string.IsNullOrEmpty(query.Status) || s.Status == query.Status)
            .Where(s => string.IsNullOrEmpty(query.WarehouseType) || s.WarehouseType == query.WarehouseType)
            .Where(s => string.IsNullOrEmpty(keyword) || s.StocktakeCode.Contains(keyword) || (s.Scope != null && s.Scope.Contains(keyword)))
            .OrderByDescending(s => s.StocktakeId)
            .Select(s => new StocktakeListItemDto(s.StocktakeId, s.StocktakeCode, s.WarehouseType, s.Scope, s.StocktakeDate,
                s.Status, s.CreatedAt, s.ApprovedAt, s.StocktakeLines.Count,
                s.StocktakeLines.Count(l => l.CountedQty != null && l.CountedQty != l.BookQty)))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<StocktakeListItemDto>>.Success(page);
    }

    public async Task<Result<StocktakeDto>> GetByIdAsync(int stocktakeId, CancellationToken ct)
    {
        var dto = await Stocktakes.AsNoTracking().Where(s => s.StocktakeId == stocktakeId)
            .Select(s => new StocktakeDto(s.StocktakeId, s.StocktakeCode, s.WarehouseType, s.Scope, s.StocktakeDate, s.Status,
                s.ReturnReason, s.CreatedAt, s.ApprovedAt,
                s.StocktakeLines.OrderBy(l => l.StocktakeLineId).Select(l => new StocktakeLineDto(l.StocktakeLineId, l.MaterialId,
                    l.Material == null ? null : l.Material.MaterialCode, l.Material == null ? null : l.Material.MaterialName,
                    l.Material == null ? null : l.Material.Unit, l.MaterialColor, l.OrderLineId,
                    l.ProductComponentId, l.ProductComponent == null ? null : l.ProductComponent.ComponentName,
                    l.OrderLine == null ? null : l.OrderLine.Order.OrderCode,
                    l.OrderLine == null ? null : l.OrderLine.ProductVariant.Product.ProductCode, l.OrderLine == null ? null : l.OrderLine.ProductVariant.Color,
                    l.OrderLine == null ? null : l.OrderLine.ProductVariant.SizeCode, l.BookQty, l.CountedQty,
                    (l.CountedQty ?? l.BookQty) - l.BookQty, l.Note)).ToList()))
            .FirstOrDefaultAsync(ct);
        return dto is null ? WarehouseFailures.NotFound<StocktakeDto>() : Result<StocktakeDto>.Success(dto);
    }

    public async Task<Result<StocktakeDto>> CreateAsync(SaveStocktakeDto dto, CancellationToken ct)
    {
        var created = await _uow.InTransactionAsync(async () =>
        {
            var lines = await BuildLinesAsync(dto, ct);
            if (!lines.IsSuccess) return WarehouseFailures.From<int>(lines);

            var stocktake = new Stocktake
            {
                StocktakeCode = await _codes.NextAsync<Stocktake>(DocumentPrefixes.Stocktake, s => s.StocktakeCode, ct),
                WarehouseType = dto.WarehouseType,
                Scope = string.IsNullOrWhiteSpace(dto.Scope) ? null : dto.Scope.Trim(),
                StocktakeDate = dto.StocktakeDate,
                Status = StocktakeStatuses.PendingApproval,
                CreatedAt = _clock.Now,
                StocktakeLines = lines.Data!,
            };
            await _uow.Repository<Stocktake>().AddAsync(stocktake);
            await NotifyApproversAsync(stocktake.StocktakeCode, ct);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(stocktake.StocktakeId, WarehouseMessages.StocktakeCreated);
        });
        return created.IsSuccess ? await GetByIdAsync(created.Data, ct) : WarehouseFailures.From<StocktakeDto>(created);
    }

    public async Task<Result<StocktakeDto>> UpdateAsync(int stocktakeId, SaveStocktakeDto dto, CancellationToken ct)
    {
        var updated = await _uow.InTransactionAsync(async () =>
        {
            var stocktake = await Stocktakes.Include(s => s.StocktakeLines).FirstOrDefaultAsync(s => s.StocktakeId == stocktakeId, ct);
            if (stocktake is null) return WarehouseFailures.NotFound<int>();
            if (stocktake.Status != StocktakeStatuses.Returned) return WarehouseFailures.InvalidStatus<int>();
            if (dto.WarehouseType != stocktake.WarehouseType)
                return Result<int>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.StocktakeWarehouseLocked);

            var lines = await BuildLinesAsync(dto, ct);
            if (!lines.IsSuccess) return WarehouseFailures.From<int>(lines);

            _uow.Repository<StocktakeLine>().RemoveRange(stocktake.StocktakeLines.ToList());
            stocktake.StocktakeLines = lines.Data!;
            stocktake.Scope = string.IsNullOrWhiteSpace(dto.Scope) ? null : dto.Scope.Trim();
            stocktake.StocktakeDate = dto.StocktakeDate;
            stocktake.Status = StocktakeStatuses.PendingApproval;
            stocktake.ReturnReason = null;
            await NotifyApproversAsync(stocktake.StocktakeCode, ct);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(stocktake.StocktakeId, WarehouseMessages.StocktakeCreated);
        });
        return updated.IsSuccess ? await GetByIdAsync(stocktakeId, ct) : WarehouseFailures.From<StocktakeDto>(updated);
    }

    public Task<Result> ApproveAsync(int stocktakeId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var stocktake = await Stocktakes.Include(s => s.StocktakeLines).FirstOrDefaultAsync(s => s.StocktakeId == stocktakeId, ct);
            if (stocktake is null) return WarehouseFailures.NotFound();
            if (stocktake.Status != StocktakeStatuses.PendingApproval) return WarehouseFailures.InvalidStatus();

            var today = _clock.Today;
            var movements = stocktake.StocktakeLines.Where(l => l.CountedQty is not null && l.CountedQty != l.BookQty)
                .Select(l => new StockMovement(KeyOf(stocktake.WarehouseType, l), l.CountedQty!.Value - l.BookQty,
                    StockDocTypes.Stocktake, stocktake.StocktakeId, l.StocktakeLineId, today)).ToList();
            if (movements.Count > 0)
            {
                var posted = await _stock.PostAsync(movements, ct);
                if (!posted.IsSuccess) return posted;
            }

            stocktake.Status = StocktakeStatuses.Approved;
            stocktake.ApprovedAt = _clock.Now;
            _audit.Add(new AuditEntry(nameof(Stocktake), stocktake.StocktakeId, AuditActions.Approve,
                new { Status = StocktakeStatuses.PendingApproval }, new { stocktake.Status, AdjustedLines = movements.Count }));
            await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
                "Phiếu kiểm kê đã được duyệt", $"Phiếu {stocktake.StocktakeCode} đã duyệt, tồn kho đã điều chỉnh theo số đếm.", null), ct);
            await _uow.SaveChangesAsync();
            return Result.Success(movements.Count == 0 ? WarehouseMessages.StocktakeApprovedNoChange : WarehouseMessages.StocktakeApproved);
        });

    public async Task<Result> ReturnAsync(int stocktakeId, ReasonDto dto, CancellationToken ct)
    {
        var stocktake = await Stocktakes.FirstOrDefaultAsync(s => s.StocktakeId == stocktakeId, ct);
        if (stocktake is null) return WarehouseFailures.NotFound();
        if (stocktake.Status != StocktakeStatuses.PendingApproval) return WarehouseFailures.InvalidStatus();

        stocktake.Status = StocktakeStatuses.Returned;
        stocktake.ReturnReason = dto.Reason.Trim();
        _audit.Add(new AuditEntry(nameof(Stocktake), stocktake.StocktakeId, AuditActions.Reject,
            new { Status = StocktakeStatuses.PendingApproval }, new { stocktake.Status }, stocktake.ReturnReason));
        await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
            "Phiếu kiểm kê bị trả lại", $"Phiếu {stocktake.StocktakeCode} bị trả lại: {stocktake.ReturnReason}", null), ct);
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.StocktakeReturned);
    }

    // ---------- Nội bộ ----------

    private static StockKey KeyOf(string warehouseType, StocktakeLine l) =>
        warehouseType == WarehouseTypes.Material
            ? StockKey.Material(l.MaterialId!.Value, l.MaterialColor ?? string.Empty)
            : warehouseType == WarehouseTypes.SemiFinished ? StockKey.SemiFinished(l.OrderLineId!.Value, l.ProductComponentId) : StockKey.Finished(l.OrderLineId!.Value);

    private async Task NotifyApproversAsync(string code, CancellationToken ct)
    {
        var message = new NotificationMessage(NotificationSources.Warehouse, "Có phiếu kiểm kê chờ duyệt",
            $"Thủ kho đã gửi phiếu {code}, vui lòng kiểm tra và duyệt.", null);
        await _notifier.NotifyRoleAsync(RoleCodes.Manager, message, ct);
        await _notifier.NotifyRoleAsync(RoleCodes.Admin, message, ct);
    }

    /// <summary>Kiểm tra từng dòng và chụp số sổ (tồn thực hiện tại) cho từng dòng.</summary>
    private async Task<Result<List<StocktakeLine>>> BuildLinesAsync(SaveStocktakeDto dto, CancellationToken ct)
    {
        if (dto.WarehouseType is not (WarehouseTypes.Material or WarehouseTypes.SemiFinished or WarehouseTypes.Finished))
            return Result<List<StocktakeLine>>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.StocktakeWarehouseInvalid);
        if (dto.StocktakeDate > _clock.Today)
            return Result<List<StocktakeLine>>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.StocktakeDateFuture);

        var isMaterial = dto.WarehouseType == WarehouseTypes.Material;
        var keys = dto.Lines.Select(l => isMaterial ? $"{l.MaterialId}|{l.MaterialColor?.Trim()}" : $"{l.OrderLineId}|{l.ProductComponentId}").ToList();
        if (keys.Distinct().Count() != keys.Count)
            return Result<List<StocktakeLine>>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DuplicateLine);
        if (dto.Lines.Any(l => isMaterial ? l.MaterialId is null || l.OrderLineId is not null : l.OrderLineId is null || l.MaterialId is not null))
            return Result<List<StocktakeLine>>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.StocktakeLineTarget);

        if (isMaterial)
        {
            var ids = dto.Lines.Select(l => l.MaterialId!.Value).Distinct().ToList();
            var count = await _uow.Repository<Material>().Query().CountAsync(m => ids.Contains(m.MaterialId), ct);
            if (count != ids.Count) return WarehouseFailures.NotFound<List<StocktakeLine>>();
        }
        else
        {
            var ids = dto.Lines.Select(l => l.OrderLineId!.Value).Distinct().ToList();
            var count = await _uow.Repository<OrderLine>().Query().CountAsync(l => ids.Contains(l.OrderLineId), ct);
            if (count != ids.Count) return WarehouseFailures.NotFound<List<StocktakeLine>>();
        }

        var lines = new List<StocktakeLine>(dto.Lines.Count);
        foreach (var l in dto.Lines)
        {
            if (l.CountedQty is < 0)
                return Result<List<StocktakeLine>>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.QtyMustNotBeNegative);
            var key = isMaterial
                ? StockKey.Material(l.MaterialId!.Value, l.MaterialColor?.Trim() ?? string.Empty)
                : dto.WarehouseType == WarehouseTypes.SemiFinished ? StockKey.SemiFinished(l.OrderLineId!.Value, l.ProductComponentId) : StockKey.Finished(l.OrderLineId!.Value);
            lines.Add(new StocktakeLine
            {
                MaterialId = l.MaterialId,
                MaterialColor = isMaterial ? l.MaterialColor?.Trim() ?? string.Empty : null,
                OrderLineId = l.OrderLineId,
                ProductComponentId = dto.WarehouseType == WarehouseTypes.SemiFinished ? l.ProductComponentId : null,
                BookQty = await _stockRepo.GetOnHandAsync(key, ct),
                CountedQty = l.CountedQty,
                Note = string.IsNullOrWhiteSpace(l.Note) ? null : l.Note.Trim(),
            });
        }
        return Result<List<StocktakeLine>>.Success(lines);
    }
}
