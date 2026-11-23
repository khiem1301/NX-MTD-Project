using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services;

/// <summary>Đơn Sửa máy (DEV-ALL-169): tổ cắt / may báo máy hỏng, chỉ có tiêu đề, nội dung, ảnh; Cơ điện xử lý.</summary>
public sealed class MachineRepairRequestHandler : NoLineRequestHandler
{
    public MachineRepairRequestHandler(IClock clock) : base(clock, Messages.MachineRepairNoLines) { }

    public override string RequestType => RequestTypes.MachineRepair;
}
