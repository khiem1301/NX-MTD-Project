using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Common;

/// <summary>DEV-ALL-49: người thuộc tổ may chỉ làm việc với đơn + mã hàng được chia cho tổ. Quản đốc, Admin và bộ phận khác không bị giới hạn.</summary>
public static class SewingTeamScope
{
    /// <summary>Tổ may cần giới hạn theo việc chia; <c>null</c> là không giới hạn.</summary>
    public static int? Of(ICurrentUser user) =>
        user.Role is not (Roles.Admin or Roles.Manager) && user.TeamTypeCode == TeamTypes.Sewing ? user.TeamId : null;
}
