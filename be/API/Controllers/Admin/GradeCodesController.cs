using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC15: Admin quản lý danh mục bậc thợ (A, B, C, D...): thêm, sửa tên / thứ tự, ngừng dùng, xóa bậc chưa ai dùng.</summary>
public sealed class GradeCodesController : AdminBaseController
{
    private readonly IGradeService _grades;

    public GradeCodesController(IGradeService grades) => _grades = grades;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => this.ToResponse(await _grades.GetCodesAsync(false, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveGradeCodeDto dto, CancellationToken ct) =>
        this.ToResponse(await _grades.CreateCodeAsync(dto, ct));

    [HttpPut("{code}")]
    public async Task<IActionResult> Update(string code, [FromBody] UpdateGradeCodeDto dto, CancellationToken ct) =>
        this.ToResponse(await _grades.UpdateCodeAsync(code, dto, ct));

    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code, CancellationToken ct) => this.ToResponse(await _grades.DeleteCodeAsync(code, ct));
}
