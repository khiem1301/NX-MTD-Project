using System.Globalization;
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
    private const string FabricCodePrefix = "V-";
    private const string TrimCodePrefix = "PL-";
    private const int CodeDigits = 4;

    private readonly IUnitOfWork _uow;
    private readonly IAppLock _locks;

    public MaterialService(IUnitOfWork uow, IAppLock locks)
    {
        _uow = uow; _locks = locks;
    }

    private IQueryable<Material> Materials => _uow.Repository<Material>().Query();

    public async Task<Result<PagedResult<MaterialDto>>> GetPagedAsync(MaterialQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Materials.AsNoTracking()
            .Where(m => string.IsNullOrEmpty(keyword) || m.MaterialCode.Contains(keyword) || m.MaterialName.Contains(keyword))
            .Where(m => string.IsNullOrEmpty(query.MaterialType) || m.MaterialType == query.MaterialType)
            .Where(m => string.IsNullOrEmpty(query.Status) || m.Status == query.Status)
            .OrderBy(m => m.MaterialCode)
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

    public Task<Result<MaterialDto>> CreateAsync(CreateMaterialDto dto, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var name = dto.MaterialName.Trim();
            var color = ColorOf(dto.Color);
            if (await NameTakenAsync(name, dto.MaterialType, color, null, ct))
                return Conflict(WarehouseMessages.MaterialNameDuplicate);

            var code = string.IsNullOrWhiteSpace(dto.MaterialCode)
                ? await NextCodeAsync(dto.MaterialType, ct)
                : dto.MaterialCode.Trim().ToUpperInvariant();
            if (await Materials.AnyAsync(m => m.MaterialCode == code, ct))
                return Conflict(WarehouseMessages.MaterialCodeDuplicate);

            var material = new Material
            {
                MaterialCode = code,
                MaterialName = name,
                Category = dto.Category,
                Unit = dto.Unit.Trim(),
                Color = color,
                LengthCm = dto.LengthCm,
                WidthCm = dto.WidthCm,
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

        var name = dto.MaterialName.Trim();
        var color = dto.Color is null ? material.Color : ColorOf(dto.Color);
        if (await NameTakenAsync(name, dto.MaterialType, color, materialId, ct))
            return Conflict(WarehouseMessages.MaterialNameDuplicate);
        // Màu là một phần của NVL: đã dùng ở định mức / đơn / chứng từ / tồn kho thì phải tạo NVL mới thay vì đổi màu.
        if (color != material.Color && await IsUsedAsync(materialId, ct))
            return Result<MaterialDto>.Failure(ErrorCodes.WarehouseMaterialInUse, WarehouseMessages.MaterialColorInUse);

        material.MaterialName = name;
        material.Color = color;
        material.Category = dto.Category;
        material.Unit = dto.Unit.Trim();
        material.LengthCm = dto.LengthCm;
        material.WidthCm = dto.WidthCm;
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

    /// <summary>UQ_Material_NameType (Tên, Loại, Màu).</summary>
    private Task<bool> NameTakenAsync(string name, string type, string color, int? exceptId, CancellationToken ct) =>
        Materials.AnyAsync(m => m.MaterialName == name && m.MaterialType == type && m.Color == color
                                && (exceptId == null || m.MaterialId != exceptId), ct);

    private async Task<bool> IsUsedAsync(int materialId, CancellationToken ct) =>
        await _uow.Repository<ProductComponentMaterial>().Query().AnyAsync(x => x.MaterialId == materialId, ct)
        || await _uow.Repository<OrderMaterial>().Query().AnyAsync(x => x.MaterialId == materialId, ct)
        || await _uow.Repository<PurchaseOrderLine>().Query().AnyAsync(x => x.MaterialId == materialId, ct)
        || await _uow.Repository<MaterialReceiptLine>().Query().AnyAsync(x => x.MaterialId == materialId, ct)
        || await _uow.Repository<MaterialIssueLine>().Query().AnyAsync(x => x.MaterialId == materialId, ct)
        || await _uow.Repository<StockBalance>().Query().AnyAsync(x => x.MaterialId == materialId, ct);

    private static string ColorOf(string? color) => color?.Trim() ?? string.Empty;

    private async Task<string> NextCodeAsync(string materialType, CancellationToken ct)
    {
        var prefix = materialType == MaterialTypes.Fabric ? FabricCodePrefix : TrimCodePrefix;
        await _locks.AcquireAsync($"material-code:{prefix}", ct);
        var codes = await Materials.AsNoTracking().Where(m => m.MaterialCode.StartsWith(prefix))
            .Select(m => m.MaterialCode).ToListAsync(ct);
        var max = codes.Select(c => int.TryParse(c[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return prefix + (max + 1).ToString(new string('0', CodeDigits), CultureInfo.InvariantCulture);
    }

    private static readonly Expression<Func<Material, MaterialDto>> ToDto = m =>
        new MaterialDto(m.MaterialId, m.MaterialCode, m.MaterialName, m.MaterialType ?? string.Empty, m.Category, m.Unit,
            m.Color, m.Status, m.CreatedAt, m.LengthCm, m.WidthCm);

    private static readonly Func<Material, MaterialDto> MapDto = ToDto.Compile();

    private static Result<MaterialDto> NotFound() =>
        Result<MaterialDto>.Failure(ErrorCodes.CommonNotFound, WarehouseMessages.NotFound, ResultKind.NotFound);

    private static Result<MaterialDto> Conflict(string message) =>
        Result<MaterialDto>.Failure(ErrorCodes.WarehouseMaterialDuplicate, message);
}
