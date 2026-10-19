using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/[controller]")]
[Authorize(Roles = Roles.Admin)]
public abstract class AdminBaseController : ControllerBase { }
