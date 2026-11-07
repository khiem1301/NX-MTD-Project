using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services;

/// <summary>Giới hạn số lượng đơn xin của Cơ điện. FE giữ cùng giá trị ở <c>features/requests/constants.ts</c>.</summary>
public static class MachineRequestLimits
{
    /// <summary>Linh kiện, vật tư máy: số nguyên, tối đa 9.999 mỗi dòng.</summary>
    public const decimal MaxPartsQty = 9_999m;

    /// <summary>Máy mới: số nguyên, tối đa 99 máy mỗi dòng.</summary>
    public const decimal MaxMachineQty = 99m;
}

/// <summary>
/// Đơn xin của Cơ điện (linh kiện, máy mới, vật tư máy): có ít nhất một dòng, tối đa 50 dòng, không trùng tên hàng,
/// ngày cần không ở quá khứ (kiểm chung của <see cref="RequisitionRequestHandler"/>); số lượng mỗi dòng là số nguyên từ 1 đến trần của loại đơn.
/// </summary>
public abstract class MachineRequestHandler : RequisitionRequestHandler
{
    private readonly decimal _maxQty;

    protected MachineRequestHandler(IClock clock, decimal maxQty) : base(clock) => _maxQty = maxQty;

    /// <summary>Số lượng hợp lệ: số nguyên, từ 1 đến trần của loại đơn.</summary>
    public static bool IsQuantityValid(decimal quantity, decimal maxQty) =>
        quantity >= 1 && quantity <= maxQty && quantity == decimal.Truncate(quantity);

    protected override Task<Result> CheckLinesAsync(RequestDraft draft, CancellationToken ct) =>
        Task.FromResult(draft.Lines.Any(l => !IsQuantityValid(l.Quantity, _maxQty))
            ? Invalid(string.Format(Messages.MachineRequestQtyInvalid, _maxQty.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))))
            : Result.Success());
}

public sealed class SparePartsRequestHandler : MachineRequestHandler
{
    public SparePartsRequestHandler(IClock clock) : base(clock, MachineRequestLimits.MaxPartsQty) { }
    public override string RequestType => RequestTypes.SpareParts;
}

public sealed class MachineSuppliesRequestHandler : MachineRequestHandler
{
    public MachineSuppliesRequestHandler(IClock clock) : base(clock, MachineRequestLimits.MaxPartsQty) { }
    public override string RequestType => RequestTypes.MachineSupplies;
}

public sealed class NewMachineRequestHandler : MachineRequestHandler
{
    public NewMachineRequestHandler(IClock clock) : base(clock, MachineRequestLimits.MaxMachineQty) { }
    public override string RequestType => RequestTypes.NewMachine;
}
