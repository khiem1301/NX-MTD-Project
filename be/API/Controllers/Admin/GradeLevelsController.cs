using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC15: Admin xem và thêm mức tiền bậc (đổi tiền = thêm dòng mới theo ngày hiệu lực); sửa / xóa chỉ với mức chưa hiệu lực.</summary>
public sealed class GradeLevelsController : AdminBaseController
{
    private readonly IGradeService _grades;

    public GradeLevelsController(IGradeService grades) => _grades = grades;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => this.ToResponse(await _grades.GetLevelsAsync(ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateGradeLevelDto dto, CancellationToken ct) =>
        this.ToResponse(await _grades.UpdateLevelAsync(id, dto, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) => this.ToResponse(await _grades.DeleteLevelAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] SaveGradeLevelDto dto, CancellationToken ct) =>
        this.ToResponse(await _grades.AddLevelAsync(dto, ct));
}
