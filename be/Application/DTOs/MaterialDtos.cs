using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record MaterialDto(
    int MaterialId, string MaterialCode, string MaterialName, string MaterialType, string Category, string Unit,
    string Status, DateTime CreatedAt);

/// <summary>MaterialCode để trống → hệ thống tự sinh V-0001 (vải) / PL-0001 (phụ liệu).</summary>
public sealed record CreateMaterialDto(string? MaterialCode, string MaterialName, string MaterialType, string Category,
    string Unit);

public sealed record UpdateMaterialDto(string MaterialName, string MaterialType, string Category, string Unit);

public sealed record ChangeMaterialStatusDto(string Status);

public sealed record MaterialQuery : PageQuery
{
    public string? MaterialType { get; init; }
    public string? Status { get; init; }
}
