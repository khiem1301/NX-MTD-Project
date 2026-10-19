using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Accountant;

[ApiController]
[Route("api/v1/accountant/[controller]")]
[Authorize(Roles = Roles.Accountant)]
public abstract class AccountantBaseController : ControllerBase { }
