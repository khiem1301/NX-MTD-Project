using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Manager;

[ApiController]
[Route("api/v1/manager/[controller]")]
[Authorize(Roles = Roles.Manager)]
public abstract class ManagerBaseController : ControllerBase { }
