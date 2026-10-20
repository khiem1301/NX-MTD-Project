namespace NX_MTD.Application.DTOs;

public sealed record OrderOptionDto(int OrderId, string OrderCode, string Status, string MaterialStatus, DateOnly DueDate);

public sealed record OrderLineOptionDto(int OrderLineId, int ProductId, string ProductCode, string ProductName,
    string Color, string SizeCode, int OrderedQty);

public sealed record TeamOptionDto(int TeamId, string TeamCode, string TeamName, string TeamType);

public sealed record MaterialOptionDto(int MaterialId, string MaterialCode, string MaterialName, string MaterialType,
    string Unit);
