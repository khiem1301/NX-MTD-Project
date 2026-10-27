using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Common;

/// <summary>Tên hiển thị của một dòng định mức (chi tiết × vải): "Tay áo – Vải gió Đen". Dòng NULL = cả bộ.</summary>
public static class ComponentLabels
{
    public static string Of(string componentName, string materialName, string materialColor) =>
        string.IsNullOrEmpty(materialColor) ? $"{componentName} – {materialName}" : $"{componentName} – {materialName} {materialColor}";

    public static string? Of(ProductComponentMaterial? pcm) =>
        pcm is null ? null : Of(pcm.ProductComponent.ComponentName, pcm.Material.MaterialName, pcm.Material.Color);
}
