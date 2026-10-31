using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>Danh sách rút gọn cho dropdown ở các màn kho / cắt (chỉ đọc).</summary>
public interface ILookupService
{
    Task<IReadOnlyList<OrderOptionDto>> GetOpenOrdersAsync(string? keyword, CancellationToken ct);
    Task<IReadOnlyList<OrderLineOptionDto>> GetOrderLinesAsync(int orderId, CancellationToken ct);
    /// <summary>Chi tiết PANEL đang dùng của mã hàng mà dòng đơn thuộc về (cắt bù chọn chi tiết, không gõ tên).</summary>
    Task<IReadOnlyList<ComponentOptionDto>> GetPanelsOfOrderLineAsync(int orderLineId, CancellationToken ct);
    Task<IReadOnlyList<TeamOptionDto>> GetTeamsAsync(string? teamType, CancellationToken ct);
    Task<IReadOnlyList<MaterialOptionDto>> GetActiveMaterialsAsync(string? keyword, CancellationToken ct);
}
