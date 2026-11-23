using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services;

/// <summary>Đơn Khác (DEV-ALL-91): chỉ có nội dung, không có dòng; ngày cần không ở quá khứ.</summary>
public sealed class OtherRequestHandler : NoLineRequestHandler
{
    public OtherRequestHandler(IClock clock) : base(clock, Messages.OtherRequestNoLines) { }

    public override string RequestType => RequestTypes.Other;
}
