using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Common;

/// <summary>Các bậc thợ đang dùng, để chọn khi đề xuất / đặt bậc (tổ trưởng, Quản đốc, Admin).</summary>
[Authorize(Roles = Roles.StaffExceptWorker)]
public sealed class GradeCodeOptionsController : CommonBaseController
{
    private readonly IGradeService _grades;

    public GradeCodeOptionsController(IGradeService grades) => _grades = grades;

    [HttpGet]
    public async Task<IActionResult> Options(CancellationToken ct) => this.ToResponse(await _grades.GetCodesAsync(true, ct));
}
