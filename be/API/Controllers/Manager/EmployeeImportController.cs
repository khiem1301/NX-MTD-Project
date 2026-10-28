using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>UC17 import cho Quản đốc: nhập nhân viên từ Excel (trừ vai trò Admin / Kế toán — service chặn theo phạm vi).</summary>
public sealed class EmployeeImportController : ManagerBaseController
{
    private readonly IEmployeeImportService _import;

    public EmployeeImportController(IEmployeeImportService import) => _import = import;

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _import.PreviewAsync(stream, ct));
    }

    [HttpPost, ResponseCache(NoStore = true)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return this.ToResponse(await _import.ImportAsync(stream, ct));
    }
}
