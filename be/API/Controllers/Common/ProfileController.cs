using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Common;

/// <summary>UC03 đổi mật khẩu, UC04 hồ sơ của mình. Mọi vai trò.</summary>
[Authorize]
public sealed class ProfileController : CommonBaseController
{
    private readonly IProfileService _profile;
    public ProfileController(IProfileService profile) => _profile = profile;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => this.ToResponse(await _profile.GetAsync(ct));

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken ct) =>
        this.ToResponse(await _profile.ChangePasswordAsync(dto, ct));
}
