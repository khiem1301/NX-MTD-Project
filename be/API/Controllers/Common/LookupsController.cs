using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Common;

/// <summary>Dropdown dùng chung cho mọi vai trò đã đăng nhập (chỉ đọc).</summary>
[Authorize]
public sealed class LookupsController : CommonBaseController
{
    private readonly ILookupService _lookups;

    public LookupsController(ILookupService lookups) => _lookups = lookups;

    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] string? keyword, [FromQuery] int? sewingTeamId, CancellationToken ct) =>
        this.ToResponse(Result<IReadOnlyList<OrderOptionDto>>.Success(await _lookups.GetOpenOrdersAsync(keyword, sewingTeamId, ct)));

    [HttpGet("orders/{orderId:int}/lines")]
    public async Task<IActionResult> OrderLines(int orderId, CancellationToken ct) =>
        this.ToResponse(Result<IReadOnlyList<OrderLineOptionDto>>.Success(await _lookups.GetOrderLinesAsync(orderId, ct)));

    [HttpGet("order-lines/{orderLineId:int}/components")]
    public async Task<IActionResult> OrderLineComponents(int orderLineId, CancellationToken ct) =>
        this.ToResponse(Result<IReadOnlyList<ComponentOptionDto>>.Success(await _lookups.GetPanelsOfOrderLineAsync(orderLineId, ct)));

    [HttpGet("teams")]
    public async Task<IActionResult> Teams([FromQuery] string? teamType, CancellationToken ct) =>
        this.ToResponse(Result<IReadOnlyList<TeamOptionDto>>.Success(await _lookups.GetTeamsAsync(teamType, ct)));

    [HttpGet("materials")]
    public async Task<IActionResult> Materials([FromQuery] string? keyword, CancellationToken ct) =>
        this.ToResponse(Result<IReadOnlyList<MaterialOptionDto>>.Success(await _lookups.GetActiveMaterialsAsync(keyword, ct)));

    /// <summary>NVL phế phẩm để chọn khi trả về kho.</summary>
    [HttpGet("scrap-materials")]
    public async Task<IActionResult> ScrapMaterials(CancellationToken ct) =>
        this.ToResponse(Result<IReadOnlyList<MaterialOptionDto>>.Success(await _lookups.GetScrapMaterialsAsync(ct)));

    /// <summary>NVL chọn được ở đơn Xin NVL kèm khả dụng kho (orderId: chỉ NVL của đơn hàng).</summary>
    [HttpGet("requisition-materials")]
    public async Task<IActionResult> RequisitionMaterials([FromQuery] int? orderId, CancellationToken ct) =>
        this.ToResponse(Result<IReadOnlyList<RequisitionMaterialOptionDto>>.Success(await _lookups.GetRequisitionMaterialsAsync(orderId, ct)));

    /// <summary>Dòng đơn chọn được ở đơn Xin BTP kèm số bộ khả dụng trong kho BTP.</summary>
    [HttpGet("orders/{orderId:int}/requisition-pieces")]
    public async Task<IActionResult> RequisitionPieces(int orderId, CancellationToken ct) =>
        this.ToResponse(Result<IReadOnlyList<RequisitionPieceOptionDto>>.Success(await _lookups.GetRequisitionPiecesAsync(orderId, ct)));
}
