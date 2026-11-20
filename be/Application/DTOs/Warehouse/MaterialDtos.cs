using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

/// <param name="Color">Màu NVL (26/10); '' = không phân màu. Cùng loại vải khác màu là hai NVL.</param>
public sealed record MaterialDto(
    int MaterialId, string MaterialName, string MaterialType, string Unit,
    string Color, string Status, DateTime CreatedAt, decimal? WidthM = null);

/// <summary>MaterialName là định danh của NVL (trước đây là mã NVL; không còn trường mã riêng). Color trống = không phân màu.</summary>
/// Unit của vải luôn là m, giá trị gửi lên được bỏ qua. WidthM (khổ vải, m) bắt buộc với vải, bỏ qua với phụ liệu (DEV-ALL-102).
public sealed record CreateMaterialDto(string MaterialName, string MaterialType,
    string Unit, string? Color = null, decimal? WidthM = null);

/// <summary>Đổi màu chỉ được khi NVL chưa dùng ở định mức, NVL của đơn, chứng từ hay tồn kho.</summary>
public sealed record UpdateMaterialDto(string MaterialName, string MaterialType, string Unit,
    string? Color = null, decimal? WidthM = null);

public sealed record ChangeMaterialStatusDto(string Status);

public sealed record MaterialQuery : PageQuery
{
    public string? MaterialType { get; init; }
    public string? Status { get; init; }
}
