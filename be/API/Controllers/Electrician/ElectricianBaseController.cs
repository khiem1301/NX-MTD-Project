using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Electrician;

[ApiController]
[Route("api/v1/electrician/[controller]")]
[Authorize(Roles = Roles.Electrician)]
public abstract class ElectricianBaseController : ControllerBase { }
