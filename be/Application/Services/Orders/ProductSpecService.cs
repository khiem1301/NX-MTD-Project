using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Orders;

/// <summary>
/// UC10 (phần định mức): thành phần PANEL / TRIM, biến thể màu × size và định mức NVL của từng biến thể, khai báo một lần
/// cho mã hàng. Lưu theo kiểu "gửi đủ danh sách": mục bỏ đi được xóa khi chưa dùng, còn dùng thì chuyển sang ngừng dùng.
/// </summary>
public sealed class ProductSpecService : IProductSpecService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogService _audit;

    public ProductSpecService(IUnitOfWork uow, IAuditLogService audit)
    {
        _uow = uow; _audit = audit;
    }

    public async Task<Result<ProductSpecDto>> GetAsync(int productId, CancellationToken ct)
    {
        if (!await _uow.Repository<Product>().Query().AnyAsync(p => p.ProductId == productId, ct))
            return Fail.NotFound<ProductSpecDto>();

        var components = await _uow.Repository<ProductComponent>().Query().AsNoTracking()
            .Where(c => c.ProductId == productId).OrderBy(c => c.SortOrder).ThenBy(c => c.ProductComponentId)
            .Select(c => new { c.ProductComponentId, c.ComponentName, c.ComponentType, c.PiecesPerGarment, c.SortOrder, c.Status })
            .ToListAsync(ct);
        var variants = await _uow.Repository<ProductVariant>().Query().AsNoTracking()
            .Where(v => v.ProductId == productId).OrderBy(v => v.Color).ThenBy(v => v.SizeCode)
            .Select(v => new { v.ProductVariantId, v.Color, v.SizeCode, v.Status })
            .ToListAsync(ct);
        var norms = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(n => n.ProductId == productId)
            .OrderBy(n => n.ProductComponent.SortOrder).ThenBy(n => n.ProductComponentId)
            .ThenBy(n => n.Material.MaterialCode).ThenBy(n => n.ProductVariant.Color).ThenBy(n => n.ProductVariant.SizeCode)
            .Select(n => new ProductNormDto(n.ProductComponentMaterialId, n.ProductComponent.ComponentName,
                n.ProductVariant.Color, n.ProductVariant.SizeCode, n.MaterialId, n.Material.MaterialCode,
                n.Material.MaterialName, n.Material.Unit, n.MaterialColor, n.QtyPerGarment, n.WastagePercent,
                n.LengthCm, n.WidthCm, n.SpecNote))
            .ToListAsync(ct);

        return Result<ProductSpecDto>.Success(new ProductSpecDto(productId,
            components.Select(c => new ProductComponentDto(c.ProductComponentId, c.ComponentName, CodeText.ToPascal(c.ComponentType),
                c.PiecesPerGarment, c.SortOrder, CodeText.ToPascal(c.Status))).ToList(),
            variants.Select(v => new ProductVariantDto(v.ProductVariantId, v.Color, v.SizeCode, CodeText.ToPascal(v.Status))).ToList(),
            norms));
    }

    public async Task<Result<ProductSpecDto>> SaveAsync(int productId, SaveProductSpecDto dto, CancellationToken ct)
    {
        var product = await _uow.Repository<Product>().Query().FirstOrDefaultAsync(p => p.ProductId == productId, ct);
        if (product is null) return Fail.NotFound<ProductSpecDto>();

        var invalid = await ValidateAsync(dto, ct);
        if (invalid is not null) return invalid;

        var saved = await _uow.InTransactionAsync(async () =>
        {
            var components = await _uow.Repository<ProductComponent>().Query().Where(c => c.ProductId == productId).ToListAsync(ct);
            var variants = await _uow.Repository<ProductVariant>().Query().Where(v => v.ProductId == productId).ToListAsync(ct);
            var norms = await _uow.Repository<ProductComponentMaterial>().Query().Where(n => n.ProductId == productId).ToListAsync(ct);

            // Khớp dòng gửi lên với dòng đang có: theo Id, không có Id thì theo tên (thành phần) / màu + size (biến thể).
            var componentMatch = dto.Components.Select(c => (Input: c, Entity: c.ProductComponentId is { } id
                ? components.FirstOrDefault(e => e.ProductComponentId == id)
                : components.FirstOrDefault(e => Same(e.ComponentName, c.ComponentName)))).ToList();
            var variantMatch = dto.Variants.Select(v => (Input: v, Entity: v.ProductVariantId is { } id
                ? variants.FirstOrDefault(e => e.ProductVariantId == id)
                : variants.FirstOrDefault(e => Same(e.Color, v.Color) && Same(e.SizeCode, v.SizeCode)))).ToList();
            if (componentMatch.Any(m => m.Input.ProductComponentId is not null && m.Entity is null)
                || variantMatch.Any(m => m.Input.ProductVariantId is not null && m.Entity is null))
                return Fail.NotFound<int>();

            // Bước 1: gỡ định mức cũ của các biến thể giữ lại + thành phần / biến thể bị bỏ (xóa nếu chưa dùng, còn dùng thì ngừng dùng).
            var keptComponents = componentMatch.Where(m => m.Entity is not null).Select(m => m.Entity!).ToHashSet();
            var keptVariants = variantMatch.Where(m => m.Entity is not null).Select(m => m.Entity!).ToHashSet();
            var droppedComponents = components.Where(c => !keptComponents.Contains(c)).ToList();
            var droppedVariants = variants.Where(v => !keptVariants.Contains(v)).ToList();

            var usedComponents = await UsedComponentIdsAsync(droppedComponents.Select(c => c.ProductComponentId).ToList(), ct);
            var droppedVariantIds = droppedVariants.Select(v => v.ProductVariantId).ToArray();
            var usedVariants = (await _uow.Repository<OrderLine>().Query()
                .Where(l => droppedVariantIds.Contains(l.ProductVariantId))
                .Select(l => l.ProductVariantId).Distinct().ToListAsync(ct)).ToHashSet();

            // Định mức bị gỡ: của biến thể giữ lại (sẽ ghi lại theo danh sách gửi lên), của biến thể / thành phần bị xóa.
            var keptVariantIds = keptVariants.Select(v => v.ProductVariantId).ToHashSet();
            var goneVariantIds = droppedVariantIds.Where(id => !usedVariants.Contains(id)).ToHashSet();
            var goneComponentIds = droppedComponents.Select(c => c.ProductComponentId).Where(id => !usedComponents.Contains(id)).ToHashSet();
            var removeNorms = norms.Where(n => keptVariantIds.Contains(n.ProductVariantId) || goneVariantIds.Contains(n.ProductVariantId)
                || goneComponentIds.Contains(n.ProductComponentId)).ToList();
            _uow.Repository<ProductComponentMaterial>().RemoveRange(removeNorms);
            foreach (var c in droppedComponents)
            {
                if (usedComponents.Contains(c.ProductComponentId)) c.Status = RecordStatus.Discontinued;
                else _uow.Repository<ProductComponent>().Remove(c);
            }
            foreach (var v in droppedVariants)
            {
                if (usedVariants.Contains(v.ProductVariantId)) v.Status = RecordStatus.Discontinued;
                else _uow.Repository<ProductVariant>().Remove(v);
            }
            await _uow.SaveChangesAsync();

            // Bước 2: cập nhật / thêm thành phần và biến thể để có Id cho định mức.
            var componentByName = new Dictionary<string, ProductComponent>(StringComparer.OrdinalIgnoreCase);
            foreach (var (input, entity) in componentMatch)
            {
                var c = entity ?? new ProductComponent { ProductId = productId };
                c.ComponentName = input.ComponentName.Trim();
                c.ComponentType = CodeText.ToCode(input.ComponentType);
                c.PiecesPerGarment = input.PiecesPerGarment;
                c.SortOrder = input.SortOrder;
                c.Status = RecordStatus.Active;
                if (entity is null) await _uow.Repository<ProductComponent>().AddAsync(c);
                componentByName[c.ComponentName] = c;
            }
            var variantByKey = new Dictionary<(string, string), ProductVariant>();
            foreach (var (input, entity) in variantMatch)
            {
                var v = entity ?? new ProductVariant { ProductId = productId };
                v.Color = input.Color.Trim();
                v.SizeCode = input.SizeCode.Trim().ToUpperInvariant();
                v.Status = RecordStatus.Active;
                if (entity is null) await _uow.Repository<ProductVariant>().AddAsync(v);
                variantByKey[(v.Color.ToUpperInvariant(), v.SizeCode)] = v;
            }
            await _uow.SaveChangesAsync();

            // Bước 3: ghi định mức mới.
            await _uow.Repository<ProductComponentMaterial>().AddRangeAsync(dto.Norms.Select(n => new ProductComponentMaterial
            {
                ProductId = productId,
                ProductVariantId = variantByKey[(n.Color.Trim().ToUpperInvariant(), n.SizeCode.Trim().ToUpperInvariant())].ProductVariantId,
                ProductComponentId = componentByName[n.ComponentName.Trim()].ProductComponentId,
                MaterialId = n.MaterialId,
                MaterialColor = n.MaterialColor?.Trim() ?? string.Empty,
                QtyPerGarment = n.QtyPerGarment,
                WastagePercent = n.WastagePercent,
                LengthCm = n.LengthCm,
                WidthCm = n.WidthCm,
                SpecNote = string.IsNullOrWhiteSpace(n.SpecNote) ? null : n.SpecNote.Trim(),
            }).ToList());
            _audit.Add(new AuditEntry(nameof(Product), productId, AuditActions.Update, null,
                new { Spec = new { Components = dto.Components.Count, Variants = dto.Variants.Count, Norms = dto.Norms.Count } }));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(productId, Messages.Saved);
        });
        if (!saved.IsSuccess) return Fail.As<ProductSpecDto>(saved);

        var spec = await GetAsync(productId, ct);
        return Result<ProductSpecDto>.Success(spec.Data!, Messages.Saved);
    }

    private async Task<Result<ProductSpecDto>?> ValidateAsync(SaveProductSpecDto dto, CancellationToken ct)
    {
        if (dto.Components.GroupBy(c => c.ComponentName.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            return Invalid(Messages.ProductSpecComponentDuplicate);
        if (dto.Variants.GroupBy(v => (v.Color.Trim().ToUpperInvariant(), v.SizeCode.Trim().ToUpperInvariant())).Any(g => g.Count() > 1))
            return Invalid(Messages.ProductSpecVariantDuplicate);

        var componentNames = dto.Components.Select(c => c.ComponentName.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var variantKeys = dto.Variants.Select(v => (v.Color.Trim().ToUpperInvariant(), v.SizeCode.Trim().ToUpperInvariant())).ToHashSet();
        if (dto.Norms.Any(n => !componentNames.Contains(n.ComponentName.Trim())
                               || !variantKeys.Contains((n.Color.Trim().ToUpperInvariant(), n.SizeCode.Trim().ToUpperInvariant()))))
            return Invalid(Messages.ProductSpecNormReference);
        // UQ_PCM: mỗi (biến thể, thành phần, NVL) một dòng.
        if (dto.Norms.GroupBy(n => (n.Color.Trim().ToUpperInvariant(), n.SizeCode.Trim().ToUpperInvariant(),
                n.ComponentName.Trim().ToUpperInvariant(), n.MaterialId)).Any(g => g.Count() > 1))
            return Invalid(Messages.ProductSpecNormDuplicate);

        var materialIds = dto.Norms.Select(n => n.MaterialId).Distinct().ToArray();
        var active = await _uow.Repository<Material>().Query()
            .CountAsync(m => materialIds.Contains(m.MaterialId) && m.Status == MaterialStatuses.Active, ct);
        return active == materialIds.Length ? null : Invalid(Messages.ProductSpecMaterialInvalid);
    }

    /// <summary>Thành phần đã được dùng ở lệnh cắt, đơn từ, bàn cắt, bàn giao, phiếu cấp, kiểm kê hoặc thẻ kho.</summary>
    private async Task<HashSet<int>> UsedComponentIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        var used = new HashSet<int>();
        if (ids.Count == 0) return used;
        var idArray = ids.ToArray();
        used.UnionWith(await _uow.Repository<WorkOrderLine>().Query().Where(l => l.ProductComponentId != null && idArray.Contains(l.ProductComponentId.Value))
            .Select(l => l.ProductComponentId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await _uow.Repository<RequestLine>().Query().Where(l => l.ProductComponentId != null && idArray.Contains(l.ProductComponentId.Value))
            .Select(l => l.ProductComponentId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await _uow.Repository<CuttingLayLine>().Query().Where(l => l.ProductComponentId != null && idArray.Contains(l.ProductComponentId.Value))
            .Select(l => l.ProductComponentId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await _uow.Repository<CutHandoverLine>().Query().Where(l => l.ProductComponentId != null && idArray.Contains(l.ProductComponentId.Value))
            .Select(l => l.ProductComponentId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await _uow.Repository<MaterialIssueLine>().Query().Where(l => l.ProductComponentId != null && idArray.Contains(l.ProductComponentId.Value))
            .Select(l => l.ProductComponentId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await _uow.Repository<StocktakeLine>().Query().Where(l => l.ProductComponentId != null && idArray.Contains(l.ProductComponentId.Value))
            .Select(l => l.ProductComponentId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await _uow.Repository<StockTransaction>().Query().Where(l => l.ProductComponentId != null && idArray.Contains(l.ProductComponentId.Value))
            .Select(l => l.ProductComponentId!.Value).Distinct().ToListAsync(ct));
        return used;
    }

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static Result<ProductSpecDto> Invalid(string message) =>
        Result<ProductSpecDto>.Failure(ErrorCodes.ProductSpecInvalid, message, ResultKind.Validation);
}
