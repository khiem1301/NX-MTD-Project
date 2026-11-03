using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Accountant;

/// <summary>Thêm nhanh NVL mới ngay khi nhập định mức mã hàng (UC10). Sửa / ngừng dùng vẫn do Thủ kho (UC38).</summary>
public sealed class MaterialsController : AccountantBaseController
{
    private readonly IMaterialService _materials;

    public MaterialsController(IMaterialService materials) => _materials = materials;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMaterialDto dto, CancellationToken ct) =>
        this.ToResponse(await _materials.CreateAsync(dto, ct));
}
