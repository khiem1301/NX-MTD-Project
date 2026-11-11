namespace NX_MTD.Application.DTOs;

// ---- Bìa cắt của đơn (DEV-ALL-85): phía trên giác theo tỉ lệ size của đơn; chỉ để ước mét vải khi lập lệnh cắt ----

/// <summary>Số sản phẩm size này trên một lớp vải của bìa.</summary>
public sealed record MarkerRatioDto(string SizeCode, short PiecesPerPly);

/// <param name="Status">Active / Discontinued: bìa ngừng dùng không được chọn khi ước vải.</param>
public sealed record OrderMarkerDto(int ProductMarkerId, int MarkerNo, string MarkerCode, decimal LengthM, string Status,
    IReadOnlyList<MarkerRatioDto> Ratios);

/// <summary>
/// Nhóm bìa đã khai ở mã hàng và các màu của đơn dùng nhóm đó (DEV-ALL-89): mỗi nhóm thuộc một dạng bìa, nên bìa của nhóm dùng cho
/// đúng các màu này, không chọn màu riêng cho bìa.
/// </summary>
public sealed record OrderMarkerGroupDto(int MarkerNo, IReadOnlyList<string> Colors);

/// <summary>Một mã hàng của đơn: size có trong đơn, nhóm bìa đã khai ở mã hàng (kèm màu dùng nhóm), và các bìa của đơn.</summary>
public sealed record OrderMarkerProductDto(int ProductId, string ProductCode, string ProductName, IReadOnlyList<string> Sizes,
    IReadOnlyList<OrderMarkerGroupDto> MarkerGroups, IReadOnlyList<OrderMarkerDto> Markers);

public sealed record OrderMarkersDto(int OrderId, IReadOnlyList<OrderMarkerProductDto> Products);

/// <param name="ProductMarkerId">null = bìa mới.</param>
public sealed record SaveOrderMarkerDto(int? ProductMarkerId, int MarkerNo, string MarkerCode, decimal LengthM, string Status,
    IReadOnlyList<MarkerRatioDto> Ratios);

/// <summary>Gửi đủ bìa của một mã hàng trong đơn: bìa không có trong danh sách bị xóa.</summary>
public sealed record SaveOrderMarkersDto(IReadOnlyList<SaveOrderMarkerDto> Markers);
