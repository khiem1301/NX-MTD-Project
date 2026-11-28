using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Common;

/// <summary>Một dòng nhận ít hơn số gửi: nhãn dòng, số gửi, số nhận, lý do người nhận ghi.</summary>
public sealed record ShortfallLine(string Label, decimal Sent, decimal Received, string? Reason);

/// <summary>Tên mã hàng - màu - size hoặc NVL của các dòng phiếu, để thông báo ghi rõ dòng nào bị giảm.</summary>
public sealed class ShortfallLabels
{
    private readonly Dictionary<int, string> _orderLines;
    private readonly Dictionary<int, string> _materials;

    internal ShortfallLabels(Dictionary<int, string> orderLines, Dictionary<int, string> materials)
    {
        _orderLines = orderLines;
        _materials = materials;
    }

    /// <summary>Dòng theo đơn (mã hàng - màu - size) hoặc theo NVL (mã - tên); không tìm thấy thì ghi số dòng.</summary>
    public string Of(int? orderLineId, int? materialId) =>
        orderLineId is { } o && _orderLines.TryGetValue(o, out var line) ? line
        : materialId is { } m && _materials.TryGetValue(m, out var material) ? material
        : $"Dòng #{orderLineId ?? materialId}";
}

/// <summary>
/// Thông báo cho người gửi khi người nhận xác nhận số ít hơn số gửi (DEV-ALL-196): ghi số gửi, số nhận và lý do ngay trong thông báo
/// để người gửi xem lại được mà không phải mở phiếu.
/// </summary>
public static class ShortfallNotice
{
    public const string Title = "Số nhận thấp hơn số gửi";
    private const int MaxMessageLength = 1000;
    private const string NoReason = "không ghi";

    /// <summary>Nội dung thông báo; null khi không dòng nào nhận ít hơn số gửi. Cắt ở giới hạn cột Notification.Message.</summary>
    public static string? Build(string document, IEnumerable<ShortfallLine> lines)
    {
        var reduced = lines.Where(l => l.Received < l.Sent)
            .Select(l => $"{l.Label}: gửi {l.Sent:0.###}, nhận {l.Received:0.###} (lý do: {l.Reason ?? NoReason})").ToList();
        if (reduced.Count == 0) return null;
        var text = $"{document}: {string.Join("; ", reduced)}.";
        return text.Length <= MaxMessageLength ? text : string.Concat(text.AsSpan(0, MaxMessageLength - 1), "…");
    }

    public static async Task<ShortfallLabels> LoadLabelsAsync(IUnitOfWork uow, IEnumerable<int?> orderLineIds,
        IEnumerable<int?> materialIds, CancellationToken ct)
    {
        var lineIds = orderLineIds.OfType<int>().Distinct().ToList();
        var matIds = materialIds.OfType<int>().Distinct().ToList();
        var lines = lineIds.Count == 0
            ? []
            : await uow.Repository<OrderLine>().Query().AsNoTracking().Where(l => lineIds.Contains(l.OrderLineId))
                .Select(l => new { l.OrderLineId, l.ProductVariant.Product.ProductCode, l.ProductVariant.ProductColor.Color, l.ProductVariant.SizeCode })
                .ToDictionaryAsync(l => l.OrderLineId, l => $"{l.ProductCode} {l.Color}-{l.SizeCode}", ct);
        var materials = matIds.Count == 0
            ? []
            : await uow.Repository<Material>().Query().AsNoTracking().Where(m => matIds.Contains(m.MaterialId))
                .Select(m => new { m.MaterialId, m.MaterialCode, m.MaterialName })
                .ToDictionaryAsync(m => m.MaterialId, m => $"{m.MaterialCode} {m.MaterialName}", ct);
        return new ShortfallLabels(lines, materials);
    }
}
