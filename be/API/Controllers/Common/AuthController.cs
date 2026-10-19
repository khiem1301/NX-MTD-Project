using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.API.Controllers.Common;

public sealed class AuthController : CommonBaseController
{
    private readonly IAuthService _auth;
    private readonly ICurrentUser _user;

    public AuthController(IAuthService auth, ICurrentUser user) { _auth = auth; _user = user; }

    [HttpPost("login"), AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct) =>
        this.ToResponse(await _auth.LoginAsync(dto, ct));

    [HttpPost("refresh"), AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto, CancellationToken ct) =>
        this.ToResponse(await _auth.RefreshAsync(dto, ct));

    /// <summary>Phien khong luu o BE (Quyet dinh D3): FE xoa token. Endpoint de FE goi cho du luong.</summary>
    [HttpPost("logout"), Authorize]
    public IActionResult Logout() => this.ToResponse(Result.Success(Messages.LoggedOut));

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me(CancellationToken ct) =>
        this.ToResponse(await _auth.GetMeAsync(_user.AccountId, ct));
}
