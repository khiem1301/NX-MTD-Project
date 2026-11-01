using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

/// <param name="Color">Màu NVL (26/10); '' = không phân màu. Cùng loại vải khác màu là hai NVL.</param>
public sealed record MaterialDto(
    int MaterialId, string MaterialCode, string MaterialName, string MaterialType, string Category, string Unit,
    string Color, string Status, DateTime CreatedAt, decimal? WidthCm = null);

/// <summary>MaterialCode để trống → hệ thống tự sinh V-0001 (vải) / PL-0001 (phụ liệu). Color trống = không phân màu.</summary>
/// WidthCm (cm): khổ vải, dùng ước tính vải cắt bù; chỉ nhập cho vải.
public sealed record CreateMaterialDto(string? MaterialCode, string MaterialName, string MaterialType, string Category,
    string Unit, string? Color = null, decimal? WidthCm = null);

/// <summary>Đổi màu chỉ được khi NVL chưa dùng ở định mức, NVL của đơn, chứng từ hay tồn kho.</summary>
public sealed record UpdateMaterialDto(string MaterialName, string MaterialType, string Category, string Unit,
    string? Color = null, decimal? WidthCm = null);

public sealed record ChangeMaterialStatusDto(string Status);

public sealed record MaterialQuery : PageQuery
{
    public string? MaterialType { get; init; }
    public string? Status { get; init; }
}
