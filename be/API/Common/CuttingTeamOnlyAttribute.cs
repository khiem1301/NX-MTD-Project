using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Common;

/// <summary>
/// Tổ trưởng / tổ phó tổ may cùng vai trò với tổ cắt nên [Authorize(Roles)] không phân biệt được.
/// Chức năng của tổ cắt (lệnh cắt, bàn cắt, bàn giao BTP) chỉ mở cho người thuộc tổ cắt.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CuttingTeamOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (user.TeamTypeCode == TeamTypes.Cutting) return;

        context.Result = new ObjectResult(ApiResponses.Failure(ErrorCodes.WarehouseCuttingTeamOnly, WarehouseMessages.CuttingTeamOnly))
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }
}
