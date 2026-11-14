namespace NX_MTD.Application.DTOs;

public sealed record OrderOptionDto(int OrderId, string OrderCode, string Status, string MaterialStatus, DateOnly DueDate, DateOnly ReceivedDate);

/// <summary>Dòng đơn để chọn. PlannedQty = tổng kế hoạch cắt cả bộ của các lệnh cắt thường chưa hủy (không tính lệnh cắt bù).</summary>
public sealed record OrderLineOptionDto(int OrderLineId, int ProductId, string ProductCode, string ProductName,
    string Color, string SizeCode, int OrderedQty, int PlannedQty);

/// <summary>Chi tiết cắt (PANEL) của mã hàng để chọn khi xin cắt bù một chi tiết.</summary>
public sealed record ComponentOptionDto(int ProductComponentMaterialId, string ComponentName, short PiecesPerGarment);

public sealed record TeamOptionDto(int TeamId, string TeamCode, string TeamName, string TeamType);

public sealed record MaterialOptionDto(int MaterialId, string MaterialCode, string MaterialName, string MaterialType,
    string Unit, string Color);

/// <summary>NVL chọn được ở đơn Xin NVL kèm số khả dụng trong kho (trần số lượng xin, DEV-ALL-110).</summary>
public sealed record RequisitionMaterialOptionDto(int MaterialId, string MaterialCode, string MaterialName, string MaterialType,
    string Unit, string Color, decimal AvailableQty);

/// <summary>Dòng đơn (cả bộ) chọn được ở đơn Xin BTP kèm số bộ khả dụng trong kho BTP (DEV-ALL-110).</summary>
public sealed record RequisitionPieceOptionDto(int OrderLineId, string ProductCode, string ProductName, string Color, string SizeCode,
    int OrderedQty, decimal AvailableQty);
