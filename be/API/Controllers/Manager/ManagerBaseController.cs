using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.Manager;

/// <summary>Admin có toàn bộ chức năng của Quản đốc nên gọi được mọi API ở đây.</summary>
[ApiController]
[Route("api/v1/manager/[controller]")]
[Authorize(Roles = Roles.AdminOrManager)]
public abstract class ManagerBaseController : ControllerBase { }
