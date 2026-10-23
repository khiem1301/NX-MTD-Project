using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>UC17 import: Admin nhập nhân viên từ file Excel — xem trước (MSG24) rồi nhập; mật khẩu tạm chỉ trả một lần.</summary>
public sealed class EmployeeImportController : AdminBaseController
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
