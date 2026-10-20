using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

/// <summary>Thủ kho xem NVL của đơn (chỉ đọc) để chọn dòng khi lập phiếu nhập (UC37).</summary>
public sealed class OrderMaterialsController : WarehouseKeeperBaseController
{
    private readonly IOrderMaterialService _orderMaterials;

    public OrderMaterialsController(IOrderMaterialService orderMaterials) => _orderMaterials = orderMaterials;

    [HttpGet]
    public async Task<IActionResult> GetByOrder([FromQuery] int orderId, CancellationToken ct) =>
        this.ToResponse(await _orderMaterials.GetByOrderAsync(orderId, ct));
}
