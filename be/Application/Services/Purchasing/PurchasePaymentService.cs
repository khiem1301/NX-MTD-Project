using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-117: sửa một lần thanh toán đơn mua (trong 24 giờ kể từ lúc nhập, bắt buộc có lý do) và nhập giá trị thực tế của đơn.
/// Thêm lần thanh toán nằm ở <see cref="PurchaseOrderFlowService.ConfirmPaymentAsync"/>. Không có thao tác xóa.
/// </summary>
public sealed class PurchasePaymentService : IPurchasePaymentService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;

    public PurchasePaymentService(IUnitOfWork uow, IAuditLogService audit, IClock clock)
    {
        _uow = uow; _audit = audit; _clock = clock;
    }

    public async Task<Result> UpdatePaymentAsync(int purchaseOrderId, int paymentId, UpdatePurchasePaymentDto dto, CancellationToken ct)
    {
        var po = await LoadAsync(purchaseOrderId, ct);
        if (po is null) return Fail.NotFound();
        var payment = po.PurchasePayments.FirstOrDefault(p => p.PurchasePaymentId == paymentId);
        if (payment is null) return Fail.NotFound();
        if (!PurchaseGoodsStatuses.Placed.Contains(po.GoodsStatus))
            return Result.Failure(ErrorCodes.PurchaseOrderInvalidStatus, Messages.PurchaseOrderInvalidStatus);
        if (!IsEditable(payment, _clock.Now))
            return Result.Failure(ErrorCodes.PaymentEditExpired, Messages.PaymentEditExpired);

        var method = CodeText.ToCode(dto.PaymentMethod);
        var invalid = Validate(method, dto.Amount, dto.PaymentDate)
                      ?? (string.IsNullOrWhiteSpace(dto.Reason)
                          ? Result.Failure(ErrorCodes.PaymentInvalid, Messages.PaymentEditReasonRequired, ResultKind.Validation)
                          : null);
        if (invalid is not null) return invalid;
        var others = po.PurchasePayments.Where(p => p.PurchasePaymentId != paymentId).Sum(p => p.Amount);
        if (others + dto.Amount > PurchaseAmounts.Actual(po))
            return Result.Failure(ErrorCodes.PaymentInvalid, Messages.PaymentAmountExceedsTotal, ResultKind.Validation);

        _uow.Repository<PurchaseOrder>().SetOriginalRowVersion(po, dto.RowVer);
        var before = new { payment.PaymentDate, payment.Amount, payment.PaymentMethod, payment.PaymentNote, PaymentStatus = po.PaymentStatus };
        var reason = dto.Reason.Trim();
        payment.PaymentDate = dto.PaymentDate;
        payment.Amount = dto.Amount;
        payment.PaymentMethod = method;
        payment.PaymentNote = Clean(dto.PaymentNote);
        payment.UpdatedAt = _clock.Now;
        payment.EditReason = reason;
        PurchaseAmounts.Sync(po);

        return await _uow.InTransactionAsync(async () =>
        {
            _audit.Add(new AuditEntry(nameof(PurchaseOrder), po.PurchaseOrderId, AuditActions.Update, before,
                new { payment.PaymentDate, payment.Amount, payment.PaymentMethod, payment.PaymentNote, po.PaymentStatus }, reason));
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.PaymentUpdated);
        });
    }

    public async Task<Result> SetActualAmountAsync(int purchaseOrderId, SetActualAmountDto dto, CancellationToken ct)
    {
        var po = await LoadAsync(purchaseOrderId, ct);
        if (po is null) return Fail.NotFound();
        if (!CanSetActual(po.GoodsStatus, po.PaymentStatus))
            return Result.Failure(ErrorCodes.PurchaseOrderInvalidStatus, Messages.PaymentActualNotAllowed);
        if (dto.Amount is not null)
        {
            if (dto.Amount < 0 || string.IsNullOrWhiteSpace(dto.Reason))
                return Result.Failure(ErrorCodes.PaymentInvalid,
                    dto.Amount < 0 ? Messages.PaymentAmountInvalid : Messages.PaymentActualReasonRequired, ResultKind.Validation);
            if (dto.Amount < PurchaseAmounts.Paid(po))
                return Result.Failure(ErrorCodes.PaymentInvalid, Messages.PaymentActualBelowPaid, ResultKind.Validation);
        }

        _uow.Repository<PurchaseOrder>().SetOriginalRowVersion(po, dto.RowVer);
        var before = new { po.ActualAmountOverride, po.PaymentStatus };
        po.ActualAmountOverride = dto.Amount;
        po.ActualAmountReason = dto.Amount is null ? null : dto.Reason?.Trim();
        PurchaseAmounts.Sync(po);

        return await _uow.InTransactionAsync(async () =>
        {
            _audit.Add(new AuditEntry(nameof(PurchaseOrder), po.PurchaseOrderId, AuditActions.Update, before,
                new { po.ActualAmountOverride, po.PaymentStatus }, po.ActualAmountReason));
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.PaymentActualSaved);
        });
    }

    /// <summary>Sửa được trong 24 giờ kể từ lúc nhập lần thanh toán đó.</summary>
    internal static bool IsEditable(PurchasePayment payment, DateTime now) =>
        now - payment.CreatedAt <= TimeSpan.FromHours(PurchasePaymentRules.EditWindowHours);

    /// <summary>Chỉ đơn đang nhận hàng hoặc đã chốt giao thiếu mới nhập được giá trị thực tế; đơn trả lại Quản đốc thì chưa.</summary>
    internal static bool CanSetActual(string goodsStatus, string paymentStatus) =>
        PurchaseGoodsStatuses.ActualAmountEditable.Contains(goodsStatus) && paymentStatus != PurchasePaymentStatuses.Returned;

    private Task<PurchaseOrder?> LoadAsync(int purchaseOrderId, CancellationToken ct) =>
        _uow.Repository<PurchaseOrder>().Query().Include(p => p.PurchaseOrderLines).Include(p => p.PurchasePayments)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct);

    private Result? Validate(string method, decimal amount, DateOnly date)
    {
        if (!PaymentMethods.All.Contains(method))
            return Result.Failure(ErrorCodes.PaymentInvalid, Messages.PaymentMethodInvalid, ResultKind.Validation);
        if (amount <= 0)
            return Result.Failure(ErrorCodes.PaymentInvalid, Messages.PaymentAmountInvalid, ResultKind.Validation);
        if (date > _clock.Today)
            return Result.Failure(ErrorCodes.PaymentInvalid, Messages.PaymentDateFuture, ResultKind.Validation);
        return null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
