using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Worker;

[ApiController]
[Route("api/v1/worker/[controller]")]
[Authorize(Roles = Roles.Worker)]
public abstract class WorkerBaseController : ControllerBase { }
