using Microsoft.AspNetCore.Mvc;

namespace NX_MTD.API.Controllers.Common;

[ApiController]
[Route("api/v1/common/[controller]")]
public abstract class CommonBaseController : ControllerBase { }
