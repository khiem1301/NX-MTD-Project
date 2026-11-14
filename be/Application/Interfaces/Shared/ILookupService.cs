using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>Danh sách rút gọn cho dropdown ở các màn kho / cắt (chỉ đọc).</summary>
public interface ILookupService
{
    /// <summary>Tổ may chỉ thấy đơn có dòng chia cho tổ; Quản đốc / Admin truyền sewingTeamId để lọc theo tổ đang chọn.</summary>
    Task<IReadOnlyList<OrderOptionDto>> GetOpenOrdersAsync(string? keyword, int? sewingTeamId, CancellationToken ct);
    Task<IReadOnlyList<OrderLineOptionDto>> GetOrderLinesAsync(int orderId, CancellationToken ct);
    /// <summary>Chi tiết PANEL đang dùng của mã hàng mà dòng đơn thuộc về (cắt bù chọn chi tiết, không gõ tên).</summary>
    Task<IReadOnlyList<ComponentOptionDto>> GetPanelsOfOrderLineAsync(int orderLineId, CancellationToken ct);
    Task<IReadOnlyList<TeamOptionDto>> GetTeamsAsync(string? teamType, CancellationToken ct);
    Task<IReadOnlyList<MaterialOptionDto>> GetActiveMaterialsAsync(string? keyword, CancellationToken ct);

    /// <summary>NVL phế phẩm đang dùng (vải vụn, kim gãy…) để tổ cắt / tổ may chọn khi trả về kho (DEV-ALL-112).</summary>
    Task<IReadOnlyList<MaterialOptionDto>> GetScrapMaterialsAsync(CancellationToken ct);

    /// <summary>NVL chọn được ở đơn Xin NVL kèm khả dụng kho: có đơn hàng thì chỉ NVL của đơn (OrderMaterial), không thì NVL đang dùng.</summary>
    Task<IReadOnlyList<RequisitionMaterialOptionDto>> GetRequisitionMaterialsAsync(int? orderId, CancellationToken ct);

    /// <summary>Dòng đơn chọn được ở đơn Xin BTP kèm số bộ khả dụng; tổ may chỉ thấy dòng chia cho tổ.</summary>
    Task<IReadOnlyList<RequisitionPieceOptionDto>> GetRequisitionPiecesAsync(int orderId, CancellationToken ct);
}
