using Microsoft.AspNetCore.Mvc;
using NX_MTD.API.Common;
using NX_MTD.Application.Common;

namespace NX_MTD.API.Controllers.Worker;

// TODO(Khiem, KH-04): xoa khi folder nay da co controller that. Chi de nghiem thu phan quyen theo role.
public sealed class PingController : WorkerBaseController
{
    [HttpGet]
    public IActionResult Get() => this.ToResponse(Result<string>.Success("pong worker"));
}
