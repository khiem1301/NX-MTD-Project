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
            if (await NameTakenAsync(name, dto.MaterialType, null, ct))
                return Conflict(WarehouseMessages.MaterialNameDuplicate);

            var code = string.IsNullOrWhiteSpace(dto.MaterialCode)
                ? await NextCodeAsync(dto.MaterialType, ct)
                : dto.MaterialCode.Trim().ToUpperInvariant();
            if (await Materials.AnyAsync(m => m.MaterialCode == code, ct))
                return Conflict(WarehouseMessages.MaterialCodeDuplicate);

            var material = new Material
            {
                MaterialCode = code, MaterialName = name, MaterialType = dto.MaterialType,
                Category = dto.Category, Unit = dto.Unit.Trim(), Status = MaterialStatuses.Active,
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
        if (await NameTakenAsync(name, dto.MaterialType, materialId, ct))
            return Conflict(WarehouseMessages.MaterialNameDuplicate);

        material.MaterialName = name;
        material.MaterialType = dto.MaterialType;
        material.Category = dto.Category;
        material.Unit = dto.Unit.Trim();
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

    private Task<bool> NameTakenAsync(string name, string type, int? exceptId, CancellationToken ct) =>
        Materials.AnyAsync(m => m.MaterialName == name && m.MaterialType == type
                                && (exceptId == null || m.MaterialId != exceptId), ct);

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
        new MaterialDto(m.MaterialId, m.MaterialCode, m.MaterialName, m.MaterialType, m.Category, m.Unit, m.Status,
            m.CreatedAt);

    private static readonly Func<Material, MaterialDto> MapDto = ToDto.Compile();

    private static Result<MaterialDto> NotFound() =>
        Result<MaterialDto>.Failure(ErrorCodes.CommonNotFound, WarehouseMessages.NotFound, ResultKind.NotFound);

    private static Result<MaterialDto> Conflict(string message) =>
        Result<MaterialDto>.Failure(ErrorCodes.WarehouseMaterialDuplicate, message);
}
