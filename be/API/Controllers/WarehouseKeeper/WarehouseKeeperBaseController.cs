using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.WarehouseKeeper;

[ApiController]
[Route("api/v1/warehouse-keeper/[controller]")]
[Authorize(Roles = Roles.WarehouseKeeper)]
public abstract class WarehouseKeeperBaseController : ControllerBase { }
