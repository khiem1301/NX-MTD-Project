using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>Quản đốc chỉ chọn nhà cung cấp khi đặt hàng nên chỉ xem được danh sách đang hợp tác (UC23).</summary>
public sealed class SuppliersController : ManagerBaseController
{
    private readonly ISupplierService _suppliers;

    public SuppliersController(ISupplierService suppliers) => _suppliers = suppliers;

    [HttpGet]
    public async Task<IActionResult> ActiveOptions(CancellationToken ct) =>
        this.ToResponse(await _suppliers.GetActiveOptionsAsync(ct));
}
