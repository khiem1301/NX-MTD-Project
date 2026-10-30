using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Admin;

/// <summary>Admin cấu hình giờ nhắc xác nhận và giờ chốt suất ăn (mặc định 08:15 và 08:30).</summary>
public sealed class MealSettingsController : AdminBaseController
{
    private readonly IMealSettingsService _settings;

    public MealSettingsController(IMealSettingsService settings) => _settings = settings;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => this.ToResponse(await _settings.GetAsync(ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] MealSettingsDto dto, CancellationToken ct) =>
        this.ToResponse(await _settings.UpdateAsync(dto, ct));
}
