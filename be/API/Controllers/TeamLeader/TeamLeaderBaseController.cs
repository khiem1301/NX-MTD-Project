using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Controllers.TeamLeader;

[ApiController]
[Route("api/v1/team-leader/[controller]")]
[Authorize(Roles = Roles.TeamLeaderOrDeputy)]
public abstract class TeamLeaderBaseController : ControllerBase { }
