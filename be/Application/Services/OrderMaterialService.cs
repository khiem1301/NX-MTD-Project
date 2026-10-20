using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>UC09 (phần NVL): NVL khách cấp / phải mua của đơn và định mức theo chi tiết, màu, size.</summary>
public sealed class OrderMaterialService : IOrderMaterialService
{
    private readonly IUnitOfWork _uow;

    public OrderMaterialService(IUnitOfWork uow) => _uow = uow;

    private IQueryable<OrderMaterial> OrderMaterials => _uow.Repository<OrderMaterial>().Query();

    public async Task<Result<IReadOnlyList<OrderMaterialDto>>> GetByOrderAsync(int orderId, CancellationToken ct)
    {
        var rows = await OrderMaterials.AsNoTracking().Where(om => om.OrderId == orderId)
            .Include(om => om.Material)
            .Include(om => om.OrderMaterialNorms).ThenInclude(n => n.Product)
            .OrderBy(om => om.Material.MaterialType).ThenBy(om => om.Material.MaterialCode).ThenBy(om => om.MaterialColor)
            .AsSplitQuery().ToListAsync(ct);

        var demands = await OrderDemandsAsync(orderId, ct);
        var needs = FabricRequirementCalculator.Calculate(rows.SelectMany(ToNormLines).ToList(), demands)
            .ToDictionary(n => n.OrderMaterialId, n => n.Qty);
        var received = await ReceivedQtyAsync(orderId, ct);

        return Result<IReadOnlyList<OrderMaterialDto>>.Success(rows
            .Select(om => ToDto(om, needs.GetValueOrDefault(om.OrderMaterialId), received.GetValueOrDefault(om.OrderMaterialId)))
            .ToList());
    }

    public async Task<Result<OrderMaterialDto>> CreateAsync(CreateOrderMaterialDto dto, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId, ct);
        if (order is null) return NotFound();
        if (OrderStatuses.Closed.Contains(order.Status))
            return Fail(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);

        var entity = new OrderMaterial { OrderId = dto.OrderId };
        var check = await ApplyAsync(entity, dto, ct);
        if (check is not null) return check;

        await _uow.Repository<OrderMaterial>().AddAsync(entity);
        await _uow.SaveChangesAsync();
        return await GetOneAsync(entity.OrderId, entity.OrderMaterialId, ct);
    }

    public async Task<Result<OrderMaterialDto>> UpdateAsync(int orderMaterialId, UpdateOrderMaterialDto dto,
        CancellationToken ct)
    {
        var entity = await OrderMaterials.Include(om => om.OrderMaterialNorms)
            .FirstOrDefaultAsync(om => om.OrderMaterialId == orderMaterialId, ct);
        if (entity is null) return NotFound();

        var keyChanged = entity.MaterialId != dto.MaterialId || entity.MaterialColor != Color(dto.MaterialColor);
        if (keyChanged && await HasReceiptsAsync(orderMaterialId, ct))
            return Fail(ErrorCodes.WarehouseOrderMaterialInUse, WarehouseMessages.OrderMaterialInUse);

        var check = await ApplyAsync(entity, dto, ct);
        if (check is not null) return check;

        await _uow.SaveChangesAsync();
        return await GetOneAsync(entity.OrderId, entity.OrderMaterialId, ct);
    }

    public async Task<Result> DeleteAsync(int orderMaterialId, CancellationToken ct)
    {
        var entity = await OrderMaterials.Include(om => om.OrderMaterialNorms)
            .FirstOrDefaultAsync(om => om.OrderMaterialId == orderMaterialId, ct);
        if (entity is null) return Result.Failure(ErrorCodes.CommonNotFound, WarehouseMessages.NotFound, ResultKind.NotFound);
        if (await HasReceiptsAsync(orderMaterialId, ct))
            return Result.Failure(ErrorCodes.WarehouseOrderMaterialInUse, WarehouseMessages.OrderMaterialInUse);

        _uow.Repository<OrderMaterialNorm>().RemoveRange(entity.OrderMaterialNorms);
        _uow.Repository<OrderMaterial>().Remove(entity);
        await _uow.SaveChangesAsync();
        return Result.Success();
    }

    /// <summary>Kiểm tra nghiệp vụ rồi gán dữ liệu vào entity; trả Failure nếu sai, null nếu hợp lệ.</summary>
    private async Task<Result<OrderMaterialDto>?> ApplyAsync(OrderMaterial entity, IOrderMaterialInput dto,
        CancellationToken ct)
    {
        var material = await _uow.Repository<Material>().Query().AsNoTracking()
            .FirstOrDefaultAsync(m => m.MaterialId == dto.MaterialId, ct);
        if (material is null) return NotFound();
        var color = Color(dto.MaterialColor);
        if (material.Status != MaterialStatuses.Active
            && (entity.MaterialId != dto.MaterialId || entity.OrderMaterialId == 0))
            return Fail(ErrorCodes.WarehouseMaterialInactive, WarehouseMessages.MaterialInactive);

        var duplicate = await OrderMaterials.AnyAsync(om => om.OrderId == entity.OrderId && om.MaterialId == dto.MaterialId
            && om.MaterialColor == color && om.OrderMaterialId != entity.OrderMaterialId, ct);
        if (duplicate) return Fail(ErrorCodes.WarehouseOrderMaterialDuplicate, WarehouseMessages.OrderMaterialDuplicate);

        var norms = dto.Norms ?? Array.Empty<SaveNormDto>();
        var orderProducts = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => l.OrderId == entity.OrderId).Select(l => l.ProductId).Distinct().ToListAsync(ct);
        if (norms.Any(n => !orderProducts.Contains(n.ProductId)))
            return Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.NormProductNotInOrder);

        entity.MaterialId = dto.MaterialId;
        entity.MaterialColor = color;
        entity.RequiredQty = dto.RequiredQty;
        entity.CustomerQty = dto.CustomerQty;
        entity.CustomerExpectedDate = dto.CustomerExpectedDate;
        entity.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();

        _uow.Repository<OrderMaterialNorm>().RemoveRange(entity.OrderMaterialNorms.ToList());
        entity.OrderMaterialNorms = norms.Select(n => new OrderMaterialNorm
        {
            ProductId = n.ProductId,
            ComponentName = n.ComponentName?.Trim() ?? string.Empty,
            GarmentColor = Optional(n.GarmentColor),
            SizeCode = Optional(n.SizeCode),
            QtyPerPiece = n.QtyPerPiece,
            WastagePercent = n.WastagePercent,
        }).ToList();
        return null;
    }

    private async Task<Result<OrderMaterialDto>> GetOneAsync(int orderId, int orderMaterialId, CancellationToken ct)
    {
        var all = await GetByOrderAsync(orderId, ct);
        var one = all.Data!.FirstOrDefault(x => x.OrderMaterialId == orderMaterialId);
        return one is null ? NotFound() : Result<OrderMaterialDto>.Success(one);
    }

    private async Task<List<PieceDemand>> OrderDemandsAsync(int orderId, CancellationToken ct) =>
        await _uow.Repository<OrderLine>().Query().AsNoTracking().Where(l => l.OrderId == orderId)
            .Select(l => new PieceDemand(l.ProductId, l.Color, l.SizeCode, string.Empty, l.OrderedQty))
            .ToListAsync(ct);

    private async Task<Dictionary<int, decimal>> ReceivedQtyAsync(int orderId, CancellationToken ct) =>
        await _uow.Repository<MaterialReceiptLine>().Query().AsNoTracking()
            .Where(l => l.MaterialReceipt.OrderId == orderId && l.MaterialReceipt.Status == ReceiptStatuses.Confirmed)
            .GroupBy(l => l.OrderMaterialId)
            .Select(g => new { g.Key, Qty = g.Sum(l => l.AcceptedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

    private Task<bool> HasReceiptsAsync(int orderMaterialId, CancellationToken ct) =>
        _uow.Repository<MaterialReceiptLine>().Query().AnyAsync(l => l.OrderMaterialId == orderMaterialId, ct);

    internal static IEnumerable<NormLine> ToNormLines(OrderMaterial om) =>
        om.OrderMaterialNorms.Select(n => new NormLine(om.OrderMaterialId, om.MaterialId, om.MaterialColor,
            n.ProductId, n.ComponentName, n.GarmentColor, n.SizeCode, n.QtyPerPiece, n.WastagePercent));

    private static OrderMaterialDto ToDto(OrderMaterial om, decimal normQty, decimal receivedQty) => new(
        om.OrderMaterialId, om.OrderId, om.MaterialId, om.Material.MaterialCode, om.Material.MaterialName,
        om.Material.MaterialType, om.Material.Unit, om.MaterialColor, om.RequiredQty, om.CustomerQty,
        om.CustomerExpectedDate, om.PurchaseQty, om.Source, om.Note, normQty, receivedQty,
        om.OrderMaterialNorms.OrderBy(n => n.Product.ProductCode).ThenBy(n => n.ComponentName)
            .ThenBy(n => n.GarmentColor).ThenBy(n => n.SizeCode)
            .Select(n => new OrderMaterialNormDto(n.OrderMaterialNormId, n.ProductId, n.Product.ProductCode,
                n.ComponentName, n.GarmentColor, n.SizeCode, n.QtyPerPiece, n.WastagePercent)).ToList());

    private static string Color(string? color) => color?.Trim() ?? string.Empty;
    private static string? Optional(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static Result<OrderMaterialDto> NotFound() =>
        Result<OrderMaterialDto>.Failure(ErrorCodes.CommonNotFound, WarehouseMessages.NotFound, ResultKind.NotFound);

    private static Result<OrderMaterialDto> Fail(string code, string message) =>
        Result<OrderMaterialDto>.Failure(code, message);
}
