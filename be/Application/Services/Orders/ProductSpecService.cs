using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC10 (phần định mức): thành phần PANEL / TRIM, biến thể màu × size và định mức NVL của từng biến thể, khai báo một lần
/// cho mã hàng, kèm nhóm bìa: chi tiết nằm trên nhóm nào và vải của nhóm theo màu (DEV-ALL-85; bìa theo đơn ở OrderMarkerService). Lưu theo kiểu "gửi đủ danh sách": mục bỏ đi được xóa
/// khi chưa dùng, còn dùng thì chuyển sang ngừng dùng.
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
            .Where(v => v.ProductId == productId).OrderBy(v => v.ProductColor.Color).ThenBy(v => v.SizeCode)
            .Select(v => new { v.ProductVariantId, v.ProductColor.Color, v.SizeCode, v.Status })
            .ToListAsync(ct);
        var norms = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(n => n.ProductId == productId)
            .OrderBy(n => n.ProductComponentId)
            .ThenBy(n => n.Material.MaterialCode).ThenBy(n => n.ProductVariant.ProductColor.Color).ThenBy(n => n.ProductVariant.SizeCode)
            .Select(n => new ProductNormDto(n.ProductComponentMaterialId, n.ProductComponent.ComponentName,
                n.ProductVariant.ProductColor.Color, n.ProductVariant.SizeCode, n.MaterialId, n.Material.MaterialCode,
                n.Material.MaterialName, n.Material.Unit, n.Material.Color, n.SpecNote, n.Material.MaterialType ?? ""))
            .ToListAsync(ct);
        var markers = await _uow.Repository<ProductMarkerComponent>().Query().AsNoTracking()
            .Where(m => m.ProductId == productId).OrderBy(m => m.MarkerNo).ThenBy(m => m.ProductComponentId)
            .Select(m => new ProductMarkerComponentDto(m.MarkerNo, m.ProductComponent.ComponentName))
            .ToListAsync(ct);
        var markerFabrics = await _uow.Repository<ProductMarkerFabric>().Query().AsNoTracking()
            .Where(f => f.ProductId == productId).OrderBy(f => f.MarkerNo).ThenBy(f => f.ProductColor.Color)
            .Select(f => new ProductMarkerFabricDto(f.MarkerNo, f.ProductColor.Color, f.MaterialId, f.Material.MaterialCode,
                f.Material.MaterialName, f.Material.Color))
            .ToListAsync(ct);

        return Result<ProductSpecDto>.Success(new ProductSpecDto(productId,
            components.Select(c => new ProductComponentDto(c.ProductComponentId, c.ComponentName, CodeText.ToPascal(c.ComponentType),
                c.PiecesPerGarment, CodeText.ToPascal(c.Status))).ToList(),
            variants.Select(v => new ProductVariantDto(v.ProductVariantId, v.Color, v.SizeCode, CodeText.ToPascal(v.Status))).ToList(),
            norms, markers, markerFabrics));
    }

    public async Task<Result<ProductSpecDto>> SaveAsync(int productId, SaveProductSpecDto dto, CancellationToken ct)
    {
        var product = await _uow.Repository<Product>().Query().FirstOrDefaultAsync(p => p.ProductId == productId, ct);
        if (product is null) return Fail.NotFound<ProductSpecDto>();

        var invalid = await ValidateAsync(productId, dto, ct);
        if (invalid is not null) return invalid;

        var saved = await _uow.InTransactionAsync(() => ApplySpecAsync(productId, dto, ct));
        if (!saved.IsSuccess) return Fail.As<ProductSpecDto>(saved);

        var spec = await GetAsync(productId, ct);
        return Result<ProductSpecDto>.Success(spec.Data!, Messages.Saved);
    }

    /// <summary>Ghi định mức trong một giao dịch: gỡ mục bỏ đi → cập nhật / thêm thành phần, biến thể → ghi định mức.</summary>
    private async Task<Result<int>> ApplySpecAsync(int productId, SaveProductSpecDto dto, CancellationToken ct)
    {
        var components = await _uow.Repository<ProductComponent>().Query().Where(c => c.ProductId == productId).ToListAsync(ct);
        var variants = await _uow.Repository<ProductVariant>().Query().Include(v => v.ProductColor)
            .Where(v => v.ProductId == productId).ToListAsync(ct);
        var norms = await _uow.Repository<ProductComponentMaterial>().Query().Where(n => n.ProductId == productId).ToListAsync(ct);

        // Khớp dòng gửi lên với dòng đang có: theo Id, không có Id thì theo tên (thành phần) / màu + size (biến thể).
        var componentMatch = dto.Components.Select(c => (Input: c, Entity: c.ProductComponentId is { } id
            ? components.FirstOrDefault(e => e.ProductComponentId == id)
            : components.FirstOrDefault(e => Same(e.ComponentName, c.ComponentName)))).ToList();
        var variantMatch = dto.Variants.Select(v => (Input: v, Entity: v.ProductVariantId is { } id
            ? variants.FirstOrDefault(e => e.ProductVariantId == id)
            : variants.FirstOrDefault(e => Same(e.ProductColor.Color, v.Color) && Same(e.SizeCode, v.SizeCode)))).ToList();
        if (componentMatch.Any(m => m.Input.ProductComponentId is not null && m.Entity is null)
            || variantMatch.Any(m => m.Input.ProductVariantId is not null && m.Entity is null))
            return Fail.NotFound<int>();
        // Màu – size lấy từ đơn hàng: không tạo mới ở màn định mức.
        if (variantMatch.Any(m => m.Entity is null)) return Fail.As<int>(Invalid(Messages.ProductSpecVariantFromOrders));

        var keptComponents = componentMatch.Where(m => m.Entity is not null).Select(m => m.Entity!).ToHashSet();
        var keptVariantIds = variantMatch.Select(m => m.Entity!.ProductVariantId).ToHashSet();
        await ClearMarkerGroupsAsync(productId, ct);   // trước khi gỡ thành phần: ProductMarkerComponent trỏ vào thành phần
        var removeNorms = await RemoveDroppedComponentsAsync(productId, components, keptComponents, norms, ct);
        var (componentByName, variantByKey) = await UpsertComponentsAndVariantsAsync(productId, componentMatch, variantMatch);
        var failed = await WriteNormsAsync(productId, dto, norms.Except(removeNorms).ToList(), keptVariantIds, componentByName, variantByKey, ct);
        if (failed is not null) return failed;
        await RemoveStaleOperationLinksAsync(productId, dto, componentByName, ct);
        await WriteMarkerGroupsAsync(productId, dto, componentByName, ct);

        _audit.Add(new AuditEntry(nameof(Product), productId, AuditActions.Update, null, new
        {
            Spec = new
            {
                Components = dto.Components.Count,
                Variants = dto.Variants.Count,
                Norms = dto.Norms.Count,
                MarkerComponents = dto.MarkerComponents.Count,
                MarkerFabrics = dto.MarkerFabrics.Count,
            },
        }));
        await _uow.SaveChangesAsync();
        return Result<int>.Success(productId, Messages.Saved);
    }

    /// <summary>
    /// Bước 1: gỡ thành phần bị bỏ (xóa nếu chưa dùng, còn dùng thì ngừng dùng) cùng định mức và liên kết công đoạn của nó.
    /// Trả về các dòng định mức đã gỡ (định mức của biến thể giữ lại sẽ ghi lại theo danh sách gửi lên).
    /// </summary>
    private async Task<List<ProductComponentMaterial>> RemoveDroppedComponentsAsync(int productId, List<ProductComponent> components,
        HashSet<ProductComponent> keptComponents, List<ProductComponentMaterial> norms, CancellationToken ct)
    {
        var droppedComponents = components.Where(c => !keptComponents.Contains(c)).ToList();
        var droppedComponentIds = droppedComponents.Select(c => c.ProductComponentId).ToHashSet();
        var usedNormIds = await UsedNormIdsAsync(norms.Where(n => droppedComponentIds.Contains(n.ProductComponentId))
            .Select(n => n.ProductComponentMaterialId).ToList(), ct);
        var usedComponents = norms.Where(n => droppedComponentIds.Contains(n.ProductComponentId)
                                              && usedNormIds.Contains(n.ProductComponentMaterialId))
            .Select(n => n.ProductComponentId).ToHashSet();

        var goneComponentIds = droppedComponentIds.Where(id => !usedComponents.Contains(id)).ToHashSet();
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
        return removeNorms;
    }

    /// <summary>Bước 2: cập nhật / thêm thành phần và biến thể để có Id cho định mức.</summary>
    private async Task<(Dictionary<string, ProductComponent> ByName, Dictionary<(string, string), ProductVariant> ByKey)>
        UpsertComponentsAndVariantsAsync(int productId,
            List<(SaveProductComponentDto Input, ProductComponent? Entity)> componentMatch,
            List<(SaveProductVariantDto Input, ProductVariant? Entity)> variantMatch)
    {
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
        foreach (var (_, entity) in variantMatch)
        {
            var v = entity!;
            variantByKey[(v.ProductColor.Color.ToUpperInvariant(), v.SizeCode)] = v;
        }
        await _uow.SaveChangesAsync();
        return (componentByName, variantByKey);
    }

    /// <summary>
    /// Bước 3: ghi định mức theo kiểu cập nhật / thêm / xóa (giữ Id: lệnh cắt, phiếu cấp, bàn cắt, tồn kho BTP
    /// trỏ vào ProductComponentMaterialId nên không thể xóa rồi tạo lại). Trả về lỗi nếu định mức bị bỏ đã được dùng.
    /// </summary>
    private async Task<Result<int>?> WriteNormsAsync(int productId, SaveProductSpecDto dto, List<ProductComponentMaterial> remainingNorms,
        HashSet<int> keptVariantIds, Dictionary<string, ProductComponent> componentByName,
        Dictionary<(string, string), ProductVariant> variantByKey, CancellationToken ct)
    {
        var keptComponentIds = componentByName.Values.Select(c => c.ProductComponentId).ToHashSet();
        var existing = remainingNorms
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
                    ProductId = productId,
                    ProductVariantId = key.Item1,
                    ProductComponentId = key.Item2,
                    MaterialId = key.Item3,
                };
                await _uow.Repository<ProductComponentMaterial>().AddAsync(row);
            }
            row.SpecNote = string.IsNullOrWhiteSpace(n.SpecNote) ? null : n.SpecNote.Trim();
        }
        var stale = existing.Where(e => !wanted.Contains(e.Key)).Select(e => e.Value).ToList();
        if (stale.Count == 0) return null;
        var inUse = await UsedNormIdsAsync(stale.Select(n => n.ProductComponentMaterialId).ToList(), ct);
        if (inUse.Count > 0) return Fail.As<int>(Invalid(Messages.ProductSpecNormInUse));
        _uow.Repository<ProductComponentMaterial>().RemoveRange(stale);
        return null;
    }

    /// <summary>
    /// Liên kết công đoạn ↔ chi tiết × vải (DEV-ALL-86) chỉ là dữ liệu tra cứu: gỡ cặp không còn trong định mức chi tiết cắt
    /// (bỏ vải khỏi chi tiết, hoặc chi tiết đổi thành phụ liệu).
    /// </summary>
    private async Task RemoveStaleOperationLinksAsync(int productId, SaveProductSpecDto dto, Dictionary<string, ProductComponent> componentByName,
        CancellationToken ct)
    {
        var panels = dto.Components.Where(c => CodeText.ToCode(c.ComponentType) == ComponentTypes.Panel)
            .Select(c => Upper(c.ComponentName)).ToHashSet();
        var kept = dto.Norms.Where(n => panels.Contains(Upper(n.ComponentName)))
            .Select(n => (componentByName[n.ComponentName.Trim()].ProductComponentId, n.MaterialId)).ToHashSet();
        var links = await _uow.Repository<OperationComponent>().Query().Where(oc => oc.ProductId == productId).ToListAsync(ct);
        _uow.Repository<OperationComponent>().RemoveRange(links.Where(l => !kept.Contains((l.ProductComponentId, l.MaterialId))).ToList());
    }

    /// <summary>Gỡ nhóm bìa của mã hàng (chi tiết trên nhóm, vải theo màu) để ghi lại theo danh sách gửi lên. Không bảng nào trỏ vào hai bảng này.</summary>
    private async Task ClearMarkerGroupsAsync(int productId, CancellationToken ct)
    {
        var components = _uow.Repository<ProductMarkerComponent>();
        var fabrics = _uow.Repository<ProductMarkerFabric>();
        components.RemoveRange(await components.Query().Where(m => m.ProductId == productId).ToListAsync(ct));
        fabrics.RemoveRange(await fabrics.Query().Where(f => f.ProductId == productId).ToListAsync(ct));
        await _uow.SaveChangesAsync();   // xóa trước rồi thêm để khỏi đụng khóa PK_ProductMarkerComponent, UQ_PMF
    }

    /// <summary>Bước 4: ghi nhóm bìa của mã hàng (DEV-ALL-85): chi tiết nằm trên nhóm nào, nhóm trải trên vải nào theo màu áo.</summary>
    private async Task WriteMarkerGroupsAsync(int productId, SaveProductSpecDto dto, Dictionary<string, ProductComponent> componentByName,
        CancellationToken ct)
    {
        // Màu của nhóm bìa đã kiểm nằm trong danh sách màu – size (đều do đơn hàng tạo) nên luôn có ProductColor.
        var colorIds = (await _uow.Repository<ProductColor>().Query().Where(c => c.ProductId == productId).ToListAsync(ct))
            .ToDictionary(c => Upper(c.Color), c => c.ProductColorId);

        await _uow.Repository<ProductMarkerComponent>().AddRangeAsync(dto.MarkerComponents.Select(m => new ProductMarkerComponent
        {
            ProductId = productId,
            MarkerNo = (byte)m.MarkerNo,
            ProductComponentId = componentByName[m.ComponentName.Trim()].ProductComponentId,
        }).ToList());
        await _uow.Repository<ProductMarkerFabric>().AddRangeAsync(dto.MarkerFabrics.Select(f => new ProductMarkerFabric
        {
            ProductId = productId,
            MarkerNo = (byte)f.MarkerNo,
            ProductColorId = colorIds[Upper(f.Color)],
            MaterialId = f.MaterialId,
        }).ToList());
    }

    private async Task<Result<ProductSpecDto>?> ValidateAsync(int productId, SaveProductSpecDto dto, CancellationToken ct)
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
        if (active != materialIds.Length) return Invalid(Messages.ProductSpecMaterialInvalid);
        return await ValidateMarkerGroupsAsync(productId, dto, ct);
    }

    /// <summary>
    /// Nhóm bìa (DEV-ALL-85): không trùng; chi tiết là PANEL của mã hàng, màu có trong màu – size; nhóm nào cũng có cả chi tiết và vải;
    /// đã khai nhóm thì mọi PANEL nằm trên ít nhất một nhóm; vải có trong định mức chi tiết cắt của màu đó; nhóm đang có bìa của đơn không được bỏ.
    /// </summary>
    private async Task<Result<ProductSpecDto>?> ValidateMarkerGroupsAsync(int productId, SaveProductSpecDto dto, CancellationToken ct)
    {
        if (dto.MarkerComponents.GroupBy(m => (m.MarkerNo, Upper(m.ComponentName))).Any(g => g.Count() > 1)
            || dto.MarkerFabrics.GroupBy(f => (f.MarkerNo, Upper(f.Color))).Any(g => g.Count() > 1))
            return Invalid(Messages.ProductSpecMarkerDuplicate);

        var panels = dto.Components.Where(c => CodeText.ToCode(c.ComponentType) == ComponentTypes.Panel)
            .Select(c => Upper(c.ComponentName)).ToHashSet();
        var colors = dto.Variants.Select(v => Upper(v.Color)).ToHashSet();
        if (dto.MarkerComponents.Any(m => !panels.Contains(Upper(m.ComponentName))) || dto.MarkerFabrics.Any(f => !colors.Contains(Upper(f.Color))))
            return Invalid(Messages.ProductSpecMarkerReference);
        var groups = dto.MarkerComponents.Select(m => m.MarkerNo).ToHashSet();
        if (!groups.SetEquals(dto.MarkerFabrics.Select(f => f.MarkerNo)))
            return Invalid(Messages.ProductSpecMarkerIncomplete);
        if (groups.Count > 0 && !panels.SetEquals(dto.MarkerComponents.Select(m => Upper(m.ComponentName))))
            return Invalid(Messages.ProductSpecPanelWithoutMarker);

        var usedGroups = await _uow.Repository<ProductMarker>().Query().Where(m => m.ProductId == productId)
            .Select(m => (int)m.MarkerNo).Distinct().ToListAsync(ct);
        if (usedGroups.Any(g => !groups.Contains(g))) return Invalid(Messages.ProductSpecMarkerGroupInUse);

        var panelNorms = dto.Norms.Where(n => panels.Contains(Upper(n.ComponentName)))
            .Select(n => (Upper(n.Color), n.MaterialId)).ToHashSet();
        if (dto.MarkerFabrics.Any(f => !panelNorms.Contains((Upper(f.Color), f.MaterialId))))
            return Invalid(Messages.ProductSpecMarkerFabricInvalid);

        var fabricIds = dto.MarkerFabrics.Select(f => f.MaterialId).Distinct().ToArray();
        var fabrics = await _uow.Repository<Material>().Query().CountAsync(m => fabricIds.Contains(m.MaterialId)
            && m.MaterialType == MaterialTypes.Fabric && m.Status == MaterialStatuses.Active, ct);
        return fabrics == fabricIds.Length ? null : Invalid(Messages.ProductSpecMarkerFabricInvalid);
    }

    private static string Upper(string value) => value.Trim().ToUpperInvariant();

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
