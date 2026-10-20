using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

public interface IMaterialService
{
    Task<Result<PagedResult<MaterialDto>>> GetPagedAsync(MaterialQuery query, CancellationToken ct);
    Task<Result<MaterialDto>> GetByIdAsync(int materialId, CancellationToken ct);
    Task<Result<MaterialDto>> CreateAsync(CreateMaterialDto dto, CancellationToken ct);
    Task<Result<MaterialDto>> UpdateAsync(int materialId, UpdateMaterialDto dto, CancellationToken ct);
    Task<Result> ChangeStatusAsync(int materialId, ChangeMaterialStatusDto dto, CancellationToken ct);
}
