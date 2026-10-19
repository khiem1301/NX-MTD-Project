using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.QaQc;

[ApiController]
[Route("api/v1/qa-qc/[controller]")]
[Authorize(Roles = Roles.QaQc)]
public abstract class QaQcBaseController : ControllerBase { }
