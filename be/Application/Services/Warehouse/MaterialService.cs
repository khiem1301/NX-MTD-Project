using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed class MaterialService : IMaterialService
{
    private readonly IUnitOfWork _uow;

    public MaterialService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    private IQueryable<Material> Materials => _uow.Repository<Material>().Query();

    public async Task<Result<PagedResult<MaterialDto>>> GetPagedAsync(MaterialQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Materials.AsNoTracking()
            .Where(m => string.IsNullOrEmpty(keyword) || m.MaterialName.Contains(keyword))
            .Where(m => string.IsNullOrEmpty(query.MaterialType) || m.MaterialType == query.MaterialType)
            .Where(m => string.IsNullOrEmpty(query.Status) || m.Status == query.Status)
            .OrderBy(m => m.MaterialName)
            .Select(ToDto)
            .ToPagedAsync(query, ct);
        return Result<PagedResult<MaterialDto>>.Success(page);
    }

    public async Task<Result<MaterialDto>> GetByIdAsync(int materialId, CancellationToken ct)
    {
        var dto = await Materials.AsNoTracking().Where(m => m.MaterialId == materialId).Select(ToDto)
            .FirstOrDefaultAsync(ct);
        return dto is null ? NotFound() : Result<MaterialDto>.Success(dto);
    }

    /// <summary>Vải luôn tính theo mét, không phụ thuộc giá trị gửi lên; phụ liệu giữ đơn vị người nhập.</summary>
    private static string UnitOf(string materialType, string? unit) =>
        materialType == MaterialTypes.Fabric ? MaterialTypes.FabricUnit : unit?.Trim() ?? string.Empty;

    public Task<Result<MaterialDto>> CreateAsync(CreateMaterialDto dto, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var name = NameOf(dto.MaterialName);
            var color = ColorOf(dto.Color);
            if (await NameTakenAsync(name, null, ct))
                return Conflict(WarehouseMessages.MaterialNameDuplicate);

            var material = new Material
            {
                MaterialName = name,
                MaterialType = dto.MaterialType,
                Unit = UnitOf(dto.MaterialType, dto.Unit),
                WidthM = WidthOf(dto.MaterialType, dto.WidthM),
                Color = color,
                Status = MaterialStatuses.Active,
            };
            await _uow.Repository<Material>().AddAsync(material);
            await _uow.SaveChangesAsync();
            return Result<MaterialDto>.Success(MapDto(material));
        });

    public async Task<Result<MaterialDto>> UpdateAsync(int materialId, UpdateMaterialDto dto, CancellationToken ct)
    {
        var material = await Materials.FirstOrDefaultAsync(m => m.MaterialId == materialId, ct);
        if (material is null) return NotFound();

        var name = NameOf(dto.MaterialName);
        var color = dto.Color is null ? material.Color : ColorOf(dto.Color);
        if (await NameTakenAsync(name, materialId, ct))
            return Conflict(WarehouseMessages.MaterialNameDuplicate);
        // Màu là một phần của NVL: đã dùng ở định mức / đơn / chứng từ / tồn kho thì phải tạo NVL mới thay vì đổi màu.
        if (color != material.Color && await IsUsedAsync(materialId, ct))
            return Result<MaterialDto>.Failure(ErrorCodes.WarehouseMaterialInUse, WarehouseMessages.MaterialColorInUse);

        material.MaterialName = name;
        material.Color = color;
        material.MaterialType = dto.MaterialType;
        material.Unit = UnitOf(dto.MaterialType, dto.Unit);
        material.WidthM = WidthOf(dto.MaterialType, dto.WidthM);
        await _uow.SaveChangesAsync();
        return Result<MaterialDto>.Success(MapDto(material));
    }

    public async Task<Result> ChangeStatusAsync(int materialId, ChangeMaterialStatusDto dto, CancellationToken ct)
    {
        var material = await Materials.FirstOrDefaultAsync(m => m.MaterialId == materialId, ct);
        if (material is null) return Result.Failure(ErrorCodes.CommonNotFound, WarehouseMessages.NotFound, ResultKind.NotFound);

        material.Status = dto.Status;
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.MaterialStatusChanged);
    }

    /// <summary>UQ_Material_Name: tên NVL là định danh duy nhất (đã gồm màu trong tên nếu cần phân biệt).</summary>
    private Task<bool> NameTakenAsync(string name, int? exceptId, CancellationToken ct) =>
        Materials.AnyAsync(m => m.MaterialName == name && (exceptId == null || m.MaterialId != exceptId), ct);

    private async Task<bool> IsUsedAsync(int materialId, CancellationToken ct) =>
        await _uow.Repository<ProductComponentMaterial>().Query().AnyAsync(x => x.MaterialId == materialId, ct)
        || await _uow.Repository<OrderMaterial>().Query().AnyAsync(x => x.MaterialId == materialId, ct)
        || await _uow.Repository<PurchaseOrderLine>().Query().AnyAsync(x => x.MaterialId == materialId, ct)
        || await _uow.Repository<MaterialReceiptLine>().Query().AnyAsync(x => x.MaterialId == materialId, ct)
        || await _uow.Repository<MaterialIssueLine>().Query().AnyAsync(x => x.MaterialId == materialId, ct)
        || await _uow.Repository<StockBalance>().Query().AnyAsync(x => x.MaterialId == materialId, ct);

    /// <summary>Tên NVL lưu chữ hoa, đã Trim: UQ_Material_Name không phân biệt hoa thường nên viết khác chữ vẫn là trùng.</summary>
    private static string NameOf(string name) => name.Trim().ToUpperInvariant();

    private static string ColorOf(string? color) => color?.Trim() ?? string.Empty;

    /// <summary>Khổ chỉ có ở vải (DEV-ALL-102); phụ liệu luôn NULL.</summary>
    private static decimal? WidthOf(string materialType, decimal? widthM) =>
        materialType == MaterialTypes.Fabric ? widthM : null;

    private static readonly Expression<Func<Material, MaterialDto>> ToDto = m =>
        new MaterialDto(m.MaterialId, m.MaterialName, m.MaterialType, m.Unit,
            m.Color, m.Status, m.CreatedAt, m.WidthM);

    private static readonly Func<Material, MaterialDto> MapDto = ToDto.Compile();

    private static Result<MaterialDto> NotFound() =>
        Result<MaterialDto>.Failure(ErrorCodes.CommonNotFound, WarehouseMessages.NotFound, ResultKind.NotFound);

    private static Result<MaterialDto> Conflict(string message) =>
        Result<MaterialDto>.Failure(ErrorCodes.WarehouseMaterialDuplicate, message);
}
