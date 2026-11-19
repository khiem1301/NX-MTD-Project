using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-137: Kế toán chốt giá trị đơn hàng khách (âm = mình phải đền hợp đồng) và ghi nhận thanh toán nhiều lần
/// (số dương = khách trả, số âm = mình trả). Tổng đã thanh toán luôn nằm giữa 0 và giá trị chốt. Không xóa lần thanh toán;
/// sửa trong 24 giờ kể từ lúc nhập, bắt buộc lý do. Lịch sử ghi dưới bảng "OrderPayment" (không lẫn lịch sử sửa đơn hàng,
/// vốn có đơn giá gia công là dữ liệu mật GBR-02).
/// </summary>
public sealed class OrderPaymentService : IOrderPaymentService
{
    private const string AuditTable = "OrderPayment";

    private readonly IUnitOfWork _uow;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;

    public OrderPaymentService(IUnitOfWork uow, IAuditLogService audit, IClock clock)
    {
        _uow = uow; _audit = audit; _clock = clock;
    }

    // ---------- Xem ----------

    public async Task<Result<PagedResult<OrderPaymentListItemDto>>> ListAsync(OrderPaymentQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var state = query.State?.Trim();
        var page = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .Where(o => string.IsNullOrEmpty(keyword) || o.OrderCode.Contains(keyword) || o.Customer.CustomerName.Contains(keyword))
            .Where(o => string.IsNullOrEmpty(state)
                        || (state == OrderPaymentStates.NotSet && o.FinalAmount == null)
                        || (state == OrderPaymentStates.Settled && o.FinalAmount != null
                            && o.OrderPayments.Sum(p => p.Amount) == o.FinalAmount)
                        || (state == OrderPaymentStates.Unpaid && o.FinalAmount != null && o.FinalAmount != 0
                            && o.OrderPayments.Sum(p => p.Amount) == 0)
                        || (state == OrderPaymentStates.Partial && o.FinalAmount != null
                            && o.OrderPayments.Sum(p => p.Amount) != 0 && o.OrderPayments.Sum(p => p.Amount) != o.FinalAmount))
            .OrderByDescending(o => o.OrderId)
            .Select(o => new
            {
                o.OrderId, o.OrderCode, o.Customer.CustomerName, o.Status, o.FinalAmount,
                Paid = o.OrderPayments.Sum(p => (decimal?)p.Amount) ?? 0,
            })
            .ToPagedAsync(query, ct);

        var items = page.Items.Select(o => new OrderPaymentListItemDto(o.OrderId, o.OrderCode, o.CustomerName,
            CodeText.ToPascal(o.Status), o.FinalAmount, o.Paid, Remaining(o.FinalAmount, o.Paid), StateOf(o.FinalAmount, o.Paid))).ToList();
        return Result<PagedResult<OrderPaymentListItemDto>>.Success(new PagedResult<OrderPaymentListItemDto>(items, page.Page, page.PageSize, page.TotalItems));
    }

    public async Task<Result<OrderPaymentDto>> GetAsync(int orderId, CancellationToken ct)
    {
        var order = await LoadAsync(orderId, asNoTracking: true, ct);
        if (order is null) return Fail.NotFound<OrderPaymentDto>();
        var now = _clock.Now;
        var paid = Paid(order);
        var payments = order.OrderPayments.OrderBy(p => p.CreatedAt).ThenBy(p => p.OrderPaymentId)
            .Select(p => new OrderPaymentLineDto(p.OrderPaymentId, p.PaymentDate, p.Amount, CodeText.ToPascal(p.PaymentMethod),
                p.PaymentNote, p.CreatedAt, p.UpdatedAt, p.EditReason, IsEditable(p, now))).ToList();
        var history = await _audit.GetHistoryAsync(AuditTable, orderId, ct);
        return Result<OrderPaymentDto>.Success(new OrderPaymentDto(order.OrderId, order.OrderCode, order.Customer.CustomerName,
            CodeText.ToPascal(order.Status), order.FinalAmount, order.FinalAmountReason, paid, Remaining(order.FinalAmount, paid),
            StateOf(order.FinalAmount, paid), order.RowVer, payments, history));
    }

    // ---------- Ghi ----------

    public async Task<Result> SetFinalAmountAsync(int orderId, SetFinalAmountDto dto, CancellationToken ct)
    {
        var order = await LoadAsync(orderId, asNoTracking: false, ct);
        if (order is null) return Fail.NotFound();
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return Result.Failure(ErrorCodes.PaymentInvalid, Messages.FinalAmountReasonRequired, ResultKind.Validation);
        if (!InRange(Paid(order), dto.Amount))
            return Result.Failure(ErrorCodes.PaymentInvalid, Messages.FinalAmountBelowPaid, ResultKind.Validation);

        var before = new { order.FinalAmount };
        var reason = dto.Reason.Trim();
        Touch(order, dto.RowVer);
        order.FinalAmount = dto.Amount;
        order.FinalAmountReason = reason;
        return await SaveAsync(order, AuditActions.Update, before, new { order.FinalAmount }, reason, Messages.FinalAmountSaved);
    }

    public async Task<Result> AddPaymentAsync(int orderId, AddOrderPaymentDto dto, CancellationToken ct)
    {
        var order = await LoadAsync(orderId, asNoTracking: false, ct);
        if (order is null) return Fail.NotFound();
        if (order.FinalAmount is null)
            return Result.Failure(ErrorCodes.PaymentInvalid, Messages.OrderPaymentNeedFinalAmount);
        var method = CodeText.ToCode(dto.PaymentMethod);
        var invalid = Validate(method, dto.Amount, dto.PaymentDate)
                      ?? (InRange(Paid(order) + dto.Amount, order.FinalAmount.Value)
                          ? null
                          : Result.Failure(ErrorCodes.PaymentInvalid, Messages.OrderPaymentOutOfRange, ResultKind.Validation));
        if (invalid is not null) return invalid;

        Touch(order, dto.RowVer);
        order.OrderPayments.Add(new OrderPayment
        {
            PaymentDate = dto.PaymentDate, Amount = dto.Amount, PaymentMethod = method, PaymentNote = Clean(dto.PaymentNote), CreatedAt = _clock.Now,
        });
        return await SaveAsync(order, AuditActions.Confirm, null,
            new { dto.PaymentDate, dto.Amount, PaymentMethod = method, PaymentNote = Clean(dto.PaymentNote) }, null, Messages.OrderPaymentAdded);
    }

    public async Task<Result> UpdatePaymentAsync(int orderId, int paymentId, UpdateOrderPaymentDto dto, CancellationToken ct)
    {
        var order = await LoadAsync(orderId, asNoTracking: false, ct);
        if (order is null) return Fail.NotFound();
        var payment = order.OrderPayments.FirstOrDefault(p => p.OrderPaymentId == paymentId);
        if (payment is null || order.FinalAmount is null) return Fail.NotFound();
        if (!IsEditable(payment, _clock.Now))
            return Result.Failure(ErrorCodes.PaymentEditExpired, Messages.PaymentEditExpired);

        var method = CodeText.ToCode(dto.PaymentMethod);
        var others = order.OrderPayments.Where(p => p.OrderPaymentId != paymentId).Sum(p => p.Amount);
        var invalid = Validate(method, dto.Amount, dto.PaymentDate)
                      ?? (string.IsNullOrWhiteSpace(dto.Reason)
                          ? Result.Failure(ErrorCodes.PaymentInvalid, Messages.PaymentEditReasonRequired, ResultKind.Validation)
                          : null)
                      ?? (InRange(others + dto.Amount, order.FinalAmount.Value)
                          ? null
                          : Result.Failure(ErrorCodes.PaymentInvalid, Messages.OrderPaymentOutOfRange, ResultKind.Validation));
        if (invalid is not null) return invalid;

        var before = new { payment.PaymentDate, payment.Amount, payment.PaymentMethod, payment.PaymentNote };
        var reason = dto.Reason.Trim();
        Touch(order, dto.RowVer);
        payment.PaymentDate = dto.PaymentDate;
        payment.Amount = dto.Amount;
        payment.PaymentMethod = method;
        payment.PaymentNote = Clean(dto.PaymentNote);
        payment.UpdatedAt = _clock.Now;
        payment.EditReason = reason;
        return await SaveAsync(order, AuditActions.Update, before,
            new { payment.PaymentDate, payment.Amount, payment.PaymentMethod, payment.PaymentNote }, reason, Messages.PaymentUpdated);
    }

    // ---------- Quy tắc ----------

    /// <summary>Sửa được trong 24 giờ kể từ lúc nhập lần thanh toán đó.</summary>
    public static bool IsEditable(OrderPayment payment, DateTime now) =>
        now - payment.CreatedAt <= TimeSpan.FromHours(PurchasePaymentRules.EditWindowHours);

    /// <summary>Tổng đã thanh toán phải nằm giữa 0 và giá trị chốt (giá trị chốt âm thì nằm giữa giá trị chốt và 0).</summary>
    public static bool InRange(decimal paid, decimal finalAmount) =>
        paid >= Math.Min(0, finalAmount) && paid <= Math.Max(0, finalAmount);

    public static decimal? Remaining(decimal? finalAmount, decimal paid) => finalAmount - paid;

    public static string StateOf(decimal? finalAmount, decimal paid) =>
        finalAmount is null ? OrderPaymentStates.NotSet
        : paid == finalAmount ? OrderPaymentStates.Settled
        : paid == 0 ? OrderPaymentStates.Unpaid
        : OrderPaymentStates.Partial;

    private static decimal Paid(CustomerOrder order) => order.OrderPayments.Sum(p => p.Amount);

    private Result? Validate(string method, decimal amount, DateOnly date)
    {
        if (!PaymentMethods.All.Contains(method))
            return Result.Failure(ErrorCodes.PaymentInvalid, Messages.PaymentMethodInvalid, ResultKind.Validation);
        if (amount == 0)
            return Result.Failure(ErrorCodes.PaymentInvalid, Messages.OrderPaymentAmountInvalid, ResultKind.Validation);
        if (date > _clock.Today)
            return Result.Failure(ErrorCodes.PaymentInvalid, Messages.PaymentDateFuture, ResultKind.Validation);
        return null;
    }

    /// <summary>Đặt RowVer gốc và đánh dấu đơn đã đổi (UpdatedAt) để hai Kế toán ghi cùng lúc thì một người nhận 409.</summary>
    private void Touch(CustomerOrder order, byte[] rowVer)
    {
        _uow.Repository<CustomerOrder>().SetOriginalRowVersion(order, rowVer);
        order.UpdatedAt = _clock.Now;
    }

    private async Task<Result> SaveAsync(CustomerOrder order, string action, object? before, object after, string? reason, string message) =>
        await _uow.InTransactionAsync(async () =>
        {
            _audit.Add(new AuditEntry(AuditTable, order.OrderId, action, before, after, reason));
            await _uow.SaveChangesAsync();
            return Result.Success(message);
        });

    private Task<CustomerOrder?> LoadAsync(int orderId, bool asNoTracking, CancellationToken ct)
    {
        var query = _uow.Repository<CustomerOrder>().Query().Include(o => o.Customer).Include(o => o.OrderPayments);
        return (asNoTracking ? query.AsNoTracking() : query).FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
