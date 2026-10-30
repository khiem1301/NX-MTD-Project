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
            .Where(c => c.ProductId == productId).OrderBy(c => c.ProductComponentId)
            .Select(c => new { c.ProductComponentId, c.ComponentName, c.ComponentType, c.PiecesPerGarment, c.Status })
            .ToListAsync(ct);
        var variants = await _uow.Repository<ProductVariant>().Query().AsNoTracking()
            .Where(v => v.ProductId == productId).OrderBy(v => v.Color).ThenBy(v => v.SizeCode)
            .Select(v => new { v.ProductVariantId, v.Color, v.SizeCode, v.LengthCm, v.WidthCm, v.Status })
            .ToListAsync(ct);
        var norms = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(n => n.ProductId == productId)
            .OrderBy(n => n.ProductComponentId)
            .ThenBy(n => n.Material.MaterialCode).ThenBy(n => n.ProductVariant.Color).ThenBy(n => n.ProductVariant.SizeCode)
            .Select(n => new ProductNormDto(n.ProductComponentMaterialId, n.ProductComponent.ComponentName,
                n.ProductVariant.Color, n.ProductVariant.SizeCode, n.MaterialId, n.Material.MaterialCode,
                n.Material.MaterialName, n.Material.Unit, n.Material.Color,
                n.LengthCm, n.WidthCm, n.SpecNote))
            .ToListAsync(ct);

        return Result<ProductSpecDto>.Success(new ProductSpecDto(productId,
            components.Select(c => new ProductComponentDto(c.ProductComponentId, c.ComponentName, CodeText.ToPascal(c.ComponentType),
                c.PiecesPerGarment, CodeText.ToPascal(c.Status))).ToList(),
            variants.Select(v => new ProductVariantDto(v.ProductVariantId, v.Color, v.SizeCode, v.LengthCm, v.WidthCm, CodeText.ToPascal(v.Status))).ToList(),
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
            // Màu – size lấy từ đơn hàng: không tạo mới ở màn định mức.
            if (variantMatch.Any(m => m.Entity is null)) return Fail.As<int>(Invalid(Messages.ProductSpecVariantFromOrders));

            // Bước 1: gỡ định mức cũ của các biến thể giữ lại + thành phần / biến thể bị bỏ (xóa nếu chưa dùng, còn dùng thì ngừng dùng).
            var keptComponents = componentMatch.Where(m => m.Entity is not null).Select(m => m.Entity!).ToHashSet();
            var keptVariants = variantMatch.Where(m => m.Entity is not null).Select(m => m.Entity!).ToHashSet();
            var droppedComponents = components.Where(c => !keptComponents.Contains(c)).ToList();

            var droppedComponentIds = droppedComponents.Select(c => c.ProductComponentId).ToHashSet();
            var usedNormIds = await UsedNormIdsAsync(norms.Where(n => droppedComponentIds.Contains(n.ProductComponentId))
                .Select(n => n.ProductComponentMaterialId).ToList(), ct);
            var usedComponents = norms.Where(n => droppedComponentIds.Contains(n.ProductComponentId)
                                                  && usedNormIds.Contains(n.ProductComponentMaterialId))
                .Select(n => n.ProductComponentId).ToHashSet();

            // Định mức bị gỡ: của thành phần bị xóa (định mức của biến thể giữ lại sẽ ghi lại theo danh sách gửi lên).
            var keptVariantIds = keptVariants.Select(v => v.ProductVariantId).ToHashSet();
            var goneComponentIds = droppedComponents.Select(c => c.ProductComponentId).Where(id => !usedComponents.Contains(id)).ToHashSet();
            var removeNorms = norms.Where(n => goneComponentIds.Contains(n.ProductComponentId)).ToList();
            _uow.Repository<ProductComponentMaterial>().RemoveRange(removeNorms);
            // Công đoạn ↔ chi tiết chỉ là dữ liệu tra cứu: gỡ liên kết với thành phần bị xóa.
            var goneLinks = await _uow.Repository<OperationComponent>().Query()
                .Where(oc => oc.ProductId == productId && goneComponentIds.Contains(oc.ProductComponentId)).ToListAsync(ct);
            _uow.Repository<OperationComponent>().RemoveRange(goneLinks);
            foreach (var c in droppedComponents)
            {
                if (usedComponents.Contains(c.ProductComponentId)) c.Status = RecordStatus.Discontinued;
                else _uow.Repository<ProductComponent>().Remove(c);
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
                c.Status = RecordStatus.Active;
                if (entity is null) await _uow.Repository<ProductComponent>().AddAsync(c);
                componentByName[c.ComponentName] = c;
            }
            var variantByKey = new Dictionary<(string, string), ProductVariant>();
            foreach (var (input, entity) in variantMatch)
            {
                var v = entity!;
                v.LengthCm = input.LengthCm;
                v.WidthCm = input.WidthCm;
                variantByKey[(v.Color.ToUpperInvariant(), v.SizeCode)] = v;
            }
            await _uow.SaveChangesAsync();

            // Bước 3: ghi định mức theo kiểu cập nhật / thêm / xóa (giữ Id: lệnh cắt, phiếu cấp, bàn cắt, tồn kho BTP
            // trỏ vào ProductComponentMaterialId nên không thể xóa rồi tạo lại).
            var keptComponentIds = componentByName.Values.Select(c => c.ProductComponentId).ToHashSet();
            var existing = norms.Except(removeNorms)
                .Where(n => keptVariantIds.Contains(n.ProductVariantId) && keptComponentIds.Contains(n.ProductComponentId))
                .ToDictionary(n => (n.ProductVariantId, n.ProductComponentId, n.MaterialId));
            var wanted = new HashSet<(int, int, int)>();
            foreach (var n in dto.Norms)
            {
                var key = (variantByKey[(n.Color.Trim().ToUpperInvariant(), n.SizeCode.Trim().ToUpperInvariant())].ProductVariantId,
                    componentByName[n.ComponentName.Trim()].ProductComponentId, n.MaterialId);
                wanted.Add(key);
                var row = existing.GetValueOrDefault(key);
                if (row is null)
                {
                    row = new ProductComponentMaterial
                    {
                        ProductId = productId, ProductVariantId = key.Item1, ProductComponentId = key.Item2, MaterialId = key.Item3,
                    };
                    await _uow.Repository<ProductComponentMaterial>().AddAsync(row);
                }
                row.LengthCm = n.LengthCm;
                row.WidthCm = n.WidthCm;
                row.SpecNote = string.IsNullOrWhiteSpace(n.SpecNote) ? null : n.SpecNote.Trim();
            }
            var stale = existing.Where(e => !wanted.Contains(e.Key)).Select(e => e.Value).ToList();
            if (stale.Count > 0)
            {
                var inUse = await UsedNormIdsAsync(stale.Select(n => n.ProductComponentMaterialId).ToList(), ct);
                if (inUse.Count > 0) return Fail.As<int>(Invalid(Messages.ProductSpecNormInUse));
                _uow.Repository<ProductComponentMaterial>().RemoveRange(stale);
            }
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

    /// <summary>Dòng định mức đã được dùng ở lệnh cắt, đơn từ, bàn cắt, bàn giao, phiếu cấp hoặc dòng tồn kho BTP.</summary>
    private async Task<HashSet<int>> UsedNormIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        var used = new HashSet<int>();
        if (ids.Count == 0) return used;
        var idArray = ids.ToArray();
        used.UnionWith(await _uow.Repository<WorkOrderLine>().Query().Where(l => l.ProductComponentMaterialId != null && idArray.Contains(l.ProductComponentMaterialId.Value))
            .Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await _uow.Repository<RequestLine>().Query().Where(l => l.ProductComponentMaterialId != null && idArray.Contains(l.ProductComponentMaterialId.Value))
            .Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await _uow.Repository<CuttingLayLine>().Query().Where(l => l.ProductComponentMaterialId != null && idArray.Contains(l.ProductComponentMaterialId.Value))
            .Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await _uow.Repository<CutHandoverLine>().Query().Where(l => l.ProductComponentMaterialId != null && idArray.Contains(l.ProductComponentMaterialId.Value))
            .Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await _uow.Repository<MaterialIssueLine>().Query().Where(l => l.ProductComponentMaterialId != null && idArray.Contains(l.ProductComponentMaterialId.Value))
            .Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToListAsync(ct));
        used.UnionWith(await _uow.Repository<StockBalance>().Query().Where(l => l.ProductComponentMaterialId != null && idArray.Contains(l.ProductComponentMaterialId.Value))
            .Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToListAsync(ct));
        return used;
    }

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static Result<ProductSpecDto> Invalid(string message) =>
        Result<ProductSpecDto>.Failure(ErrorCodes.ProductSpecInvalid, message, ResultKind.Validation);
}
