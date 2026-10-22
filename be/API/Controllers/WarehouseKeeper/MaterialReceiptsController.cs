using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

/// <summary>UC37: Thủ kho lập / sửa / gửi lại phiếu nhập NVL.</summary>
public sealed class MaterialReceiptsController : WarehouseKeeperBaseController
{
    private readonly IMaterialReceiptService _receipts;

    public MaterialReceiptsController(IMaterialReceiptService receipts) => _receipts = receipts;

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] MaterialReceiptQuery query, CancellationToken ct) =>
        this.ToResponse(await _receipts.GetPagedAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        this.ToResponse(await _receipts.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveMaterialReceiptDto dto, CancellationToken ct) =>
        this.ToResponse(await _receipts.CreateAsync(dto, ct));

    /// <summary>Phiếu bổ sung: nhận hàng giao bù theo đơn thiếu NVL đã chọn "giao bù".</summary>
    [HttpPost("supplementary")]
    public async Task<IActionResult> CreateSupplementary([FromBody] SaveSupplementaryReceiptDto dto, CancellationToken ct) =>
        this.ToResponse(await _receipts.CreateSupplementaryAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveMaterialReceiptDto dto, CancellationToken ct) =>
        this.ToResponse(await _receipts.UpdateAsync(id, dto, ct));
}
