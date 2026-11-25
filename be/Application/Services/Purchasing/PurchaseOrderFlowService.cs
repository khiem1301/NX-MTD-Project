using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// MI-07 (UC23, UC34): Quản đốc chọn nhà cung cấp + chốt đặt hàng / hủy, Kế toán xác nhận thanh toán (ảnh chứng từ là
/// tùy chọn, đính qua Attachment) hoặc trả lại. Nối tiếp <see cref="PurchaseOrderService"/> (Thái) vốn chỉ tạo và xem đơn. Mọi thay đổi ghi AuditLog
/// trong cùng transaction (GBR-15); lệch RowVer → 409 do middleware.
/// </summary>
public sealed class PurchaseOrderFlowService : IPurchaseOrderFlowService
{
    private const string ManagerLink = "/purchase-orders?id={0}";
    private const string PaymentLink = "/purchase-payments?id={0}";
    private const string IncomingLink = "/warehouse/incoming-materials";

    private readonly IUnitOfWork _uow;
    private readonly IPurchaseOrderService _orders;
    private readonly IAuditLogService _audit;
    private readonly INotificationService _notifier;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly DeliveryScheduleStore _schedule;

    public PurchaseOrderFlowService(IUnitOfWork uow, IPurchaseOrderService orders, IAuditLogService audit,
        INotificationService notifier, ICurrentUser user, IClock clock)
    {
        _uow = uow; _orders = orders; _audit = audit; _notifier = notifier; _user = user; _clock = clock;
        _schedule = new DeliveryScheduleStore(uow);
    }

    private IQueryable<PurchaseOrder> PurchaseOrders => _uow.Repository<PurchaseOrder>().Query();

    // ---------- Xem ----------

    public async Task<Result<PagedResult<PurchaseOrderFlowListItemDto>>> ListAsync(PurchaseOrderFlowQuery query,
        CancellationToken ct)
    {
        var goods = string.IsNullOrWhiteSpace(query.GoodsStatus) ? null : CodeText.ToCode(query.GoodsStatus);
        var payment = string.IsNullOrWhiteSpace(query.PaymentStatus) ? null : CodeText.ToCode(query.PaymentStatus);
        var placed = PurchaseGoodsStatuses.Placed.ToArray();   // EF chỉ dịch được Contains trên mảng / List
        var keyword = query.Keyword?.Trim();

        var page = await PurchaseOrders.AsNoTracking()
            .Where(p => !query.PayableOnly || placed.Contains(p.GoodsStatus))
            .Where(p => goods == null || p.GoodsStatus == goods)
            .Where(p => payment == null || p.PaymentStatus == payment)
            .Where(p => string.IsNullOrEmpty(keyword) || p.PoCode.Contains(keyword)
                || (p.Order != null && p.Order.OrderCode.Contains(keyword))
                || (p.Supplier != null && p.Supplier.SupplierName.Contains(keyword)))
            .OrderByDescending(p => p.PurchaseOrderId)
            .Select(p => new PurchaseOrderFlowListItemDto(p.PurchaseOrderId, p.PoCode,
                p.Order == null ? null : p.Order.OrderCode, p.Supplier == null ? null : p.Supplier.SupplierName,
                p.GoodsStatus, p.PaymentStatus, p.OrderDate,
                p.PurchaseOrderLines.Sum(l => l.OrderedQty * l.UnitPrice), p.PaidAmount, p.CreatedAt))
            .ToPagedAsync(query, ct);

        return Result<PagedResult<PurchaseOrderFlowListItemDto>>.Success(page with
        {
            Items = page.Items.Select(i => i with
            {
                GoodsStatus = CodeText.ToPascal(i.GoodsStatus),
                PaymentStatus = CodeText.ToPascal(i.PaymentStatus),
            }).ToList(),
        });
    }

    public async Task<Result<PurchaseOrderFlowDto>> GetAsync(int purchaseOrderId, CancellationToken ct)
    {
        var header = await PurchaseOrders.AsNoTracking().Where(p => p.PurchaseOrderId == purchaseOrderId)
            .Select(p => new
            {
                p.PurchaseOrderId,
                p.PoCode,
                p.OrderId,
                OrderCode = p.Order == null ? null : p.Order.OrderCode,
                p.SupplierId,
                SupplierName = p.Supplier == null ? null : p.Supplier.SupplierName,
                p.GoodsStatus,
                p.PaymentStatus,
                p.OrderDate,
                p.ExpectedDeliveryCount,
                p.ExpectedDeliveryDate,
                p.ExpectedDeliveryDateTo,
                p.Note,
                p.RejectReason,
                p.PaymentReturnReason,
                p.PaymentDate,
                p.PaidAmount,
                p.PaymentMethod,
                p.PaymentNote,
                p.ActualAmount,
                p.ActualAmountSetAt,
                p.CreatedAt,
                p.RowVer,
            })
            .FirstOrDefaultAsync(ct);
        if (header is null) return Fail.NotFound<PurchaseOrderFlowDto>();
        var receivedCount = await _uow.Repository<VwPurchaseOrderDelivery>().Query().AsNoTracking()
            .Where(v => v.PurchaseOrderId == purchaseOrderId).Select(v => v.ReceivedCount).FirstOrDefaultAsync(ct);

        // Dòng + tên NVL do Thái dựng sẵn (đơn gắn đơn hàng hoặc đơn dự trữ), không viết lại.
        var detail = await _orders.GetByIdAsync(purchaseOrderId, ct);
        if (!detail.IsSuccess) return Fail.NotFound<PurchaseOrderFlowDto>();
        var lines = detail.Data!.Lines;
        var history = await _audit.GetHistoryAsync(nameof(PurchaseOrder), purchaseOrderId, ct);
        var deliveryDates = await _schedule.GetAsync(DeliveryOwner.PurchaseOrder, purchaseOrderId, ct);
        var payments = await PaymentsOfAsync(purchaseOrderId, header.GoodsStatus, ct);

        return Result<PurchaseOrderFlowDto>.Success(new PurchaseOrderFlowDto(
            header.PurchaseOrderId, header.PoCode, header.OrderId, header.OrderCode, header.SupplierId,
            header.SupplierName, CodeText.ToPascal(header.GoodsStatus), CodeText.ToPascal(header.PaymentStatus),
            header.OrderDate, header.ExpectedDeliveryCount, receivedCount, header.Note, header.RejectReason,
            header.PaymentReturnReason, header.PaymentDate, header.PaidAmount,
            header.PaymentMethod is null ? null : CodeText.ToPascal(header.PaymentMethod), header.PaymentNote,
            lines.Sum(l => l.OrderedQty * l.UnitPrice), header.CreatedAt, header.RowVer, lines,
            PermissionsFor(header.GoodsStatus, header.PaymentStatus, header.ActualAmountSetAt, detail.Data!.CanEdit), history, header.ExpectedDeliveryDate,
            header.ExpectedDeliveryDateTo, deliveryDates, header.ActualAmount,
            // DEV-ALL-129: giá trị theo số đã nhận, gợi ý Tổng giá trị thực tế cho đơn Chốt không bù.
            lines.Sum(l => l.ReceivedQty * l.UnitPrice),
            payments, header.ActualAmountSetAt));
    }

    // ---------- Quản đốc ----------

    public async Task<Result> PlaceAsync(int purchaseOrderId, PlacePurchaseOrderDto dto, CancellationToken ct)
    {
        var po = await LoadWithLinesAsync(purchaseOrderId, ct);
        if (po is null) return Fail.NotFound();
        if (po.GoodsStatus != PurchaseOrderStatuses.PendingFinalisation)
            return Result.Failure(ErrorCodes.PurchaseOrderInvalidStatus, Messages.PurchaseOrderInvalidStatus);

        var invalid = ValidatePlaceInput(po, dto);
        if (invalid is not null) return invalid;
        var dateError = await PurchaseDeliveryDate.CheckAsync(_uow, _clock, dto.ExpectedDeliveryDates, dto.OrderDate, po.OrderId, ct);
        if (dateError is not null)
            return Result.Failure(ErrorCodes.PurchaseOrderDateInvalid, dateError, ResultKind.Validation);

        var supplier = await _uow.Repository<Supplier>().Query().AsNoTracking()
            .FirstOrDefaultAsync(s => s.SupplierId == dto.SupplierId, ct);
        if (supplier is null) return Result.Failure(ErrorCodes.CommonNotFound, Messages.SupplierNotFound, ResultKind.NotFound);
        if (supplier.Status != RecordStatus.Active)
            return Result.Failure(ErrorCodes.SupplierNotActive, Messages.SupplierNotActive);

        _uow.Repository<PurchaseOrder>().SetOriginalRowVersion(po, dto.RowVer);
        var before = new { po.GoodsStatus, po.SupplierId, Lines = Snapshot(po) };

        ApplyPlacement(po, dto, supplier);

        return await _uow.InTransactionAsync(async () =>
        {
            await _schedule.ReplaceAsync(DeliveryOwner.PurchaseOrder, po.PurchaseOrderId, dto.ExpectedDeliveryDates ?? [], ct);
            _audit.Add(new AuditEntry(nameof(PurchaseOrder), po.PurchaseOrderId, AuditActions.Update, before,
                new
                {
                    po.GoodsStatus,
                    po.SupplierId,
                    po.OrderDate,
                    po.ExpectedDeliveryCount,
                    po.ExpectedDeliveryDate,
                    po.ExpectedDeliveryDateTo,
                    Lines = Snapshot(po)
                }));
            await _notifier.NotifyRoleAsync(RoleCodes.Accountant, new NotificationMessage(NotificationSources.Warehouse,
                Messages.NotifyPoPlacedTitle, string.Format(Messages.NotifyPoPlacedBody, po.PoCode, supplier.SupplierName),
                string.Format(PaymentLink, po.PurchaseOrderId), nameof(PurchaseOrder), po.PurchaseOrderId), ct);
            await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
                Messages.NotifyIncomingPoTitle, string.Format(Messages.NotifyIncomingPoBody, po.PoCode, supplier.SupplierName),
                IncomingLink, nameof(PurchaseOrder), po.PurchaseOrderId), ct);
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.PurchaseOrderPlaced);
        });
    }

    /// <summary>Dòng gửi lên phải đúng bộ dòng của đơn, đơn giá > 0, ngày đặt không ở tương lai, số lần giao ≥ 1.</summary>
    private Result? ValidatePlaceInput(PurchaseOrder po, PlacePurchaseOrderDto dto)
    {
        if (!SameLines(po, dto.Lines.Select(l => l.PurchaseOrderLineId)))
            return Result.Failure(ErrorCodes.PurchaseOrderLinesMismatch, Messages.PurchaseOrderLinesMismatch);
        if (dto.Lines.Any(l => l.UnitPrice <= 0))
            return Result.Failure(ErrorCodes.PurchaseOrderPriceRequired, Messages.PurchaseOrderPriceRequired,
                ResultKind.Validation);
        if (dto.OrderDate > _clock.Today)
            return Result.Failure(ErrorCodes.PurchaseOrderDateInvalid, Messages.PurchaseOrderOrderDateFuture,
                ResultKind.Validation);
        if (dto.ExpectedDeliveryCount < 1)
            return Result.Failure(ErrorCodes.PurchaseOrderDateInvalid, Messages.PurchaseOrderDeliveryCountInvalid,
                ResultKind.Validation);
        return null;
    }

    private void ApplyPlacement(PurchaseOrder po, PlacePurchaseOrderDto dto, Supplier supplier)
    {
        foreach (var line in po.PurchaseOrderLines)
        {
            var input = dto.Lines.Single(l => l.PurchaseOrderLineId == line.PurchaseOrderLineId);
            line.OrderedQty = input.OrderedQty;
            line.UnitPrice = input.UnitPrice;
        }
        po.SupplierId = supplier.SupplierId;
        po.OrderDate = dto.OrderDate;
        po.ExpectedDeliveryCount = dto.ExpectedDeliveryCount;
        (po.ExpectedDeliveryDate, po.ExpectedDeliveryDateTo) = DeliveryScheduleStore.Summary(dto.ExpectedDeliveryDates ?? []);
        po.Note = Clean(dto.Note) ?? po.Note;
        po.GoodsStatus = PurchaseGoodsStatuses.Ordered;
    }

    public async Task<Result> CancelAsync(int purchaseOrderId, CancelPurchaseOrderDto dto, CancellationToken ct)
    {
        var po = await _uow.Repository<PurchaseOrder>().GetByIdAsync(purchaseOrderId);
        if (po is null) return Fail.NotFound();
        if (po.GoodsStatus != PurchaseOrderStatuses.PendingFinalisation)
            return Result.Failure(ErrorCodes.PurchaseOrderInvalidStatus, Messages.PurchaseOrderInvalidStatus);

        var reason = dto.Reason.Trim();
        _uow.Repository<PurchaseOrder>().SetOriginalRowVersion(po, dto.RowVer);
        var from = po.GoodsStatus;
        po.GoodsStatus = PurchaseGoodsStatuses.Rejected;
        po.RejectReason = reason;   // CK_PO_Reject bắt buộc có lý do

        _audit.Add(new AuditEntry(nameof(PurchaseOrder), po.PurchaseOrderId, AuditActions.Reject,
            new { GoodsStatus = from }, new { po.GoodsStatus }, reason));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.PurchaseOrderCancelled);
    }

    public async Task<Result> RepriceAsync(int purchaseOrderId, RepricePurchaseOrderDto dto, CancellationToken ct)
    {
        var po = await LoadWithLinesAsync(purchaseOrderId, ct);
        if (po is null) return Fail.NotFound();
        if (!PurchaseGoodsStatuses.Repriceable.Contains(po.GoodsStatus)
            || po.PaymentStatus != PurchasePaymentStatuses.Returned)
            return Result.Failure(ErrorCodes.PurchaseOrderInvalidStatus, Messages.PurchaseOrderInvalidStatus);

        if (!SameLines(po, dto.Lines.Select(l => l.PurchaseOrderLineId)))
            return Result.Failure(ErrorCodes.PurchaseOrderLinesMismatch, Messages.PurchaseOrderLinesMismatch);
        if (dto.Lines.Any(l => l.UnitPrice <= 0))
            return Result.Failure(ErrorCodes.PurchaseOrderPriceRequired, Messages.PurchaseOrderPriceRequired,
                ResultKind.Validation);

        _uow.Repository<PurchaseOrder>().SetOriginalRowVersion(po, dto.RowVer);
        var before = new { po.PaymentStatus, po.PaymentReturnReason, po.Note, Lines = Snapshot(po) };

        foreach (var line in po.PurchaseOrderLines)
            line.UnitPrice = dto.Lines.Single(l => l.PurchaseOrderLineId == line.PurchaseOrderLineId).UnitPrice;
        po.Note = Clean(dto.Note) ?? po.Note;
        po.PaymentStatus = PurchaseOrderStatuses.Unpaid;
        po.PaymentReturnReason = null;

        return await _uow.InTransactionAsync(async () =>
        {
            _audit.Add(new AuditEntry(nameof(PurchaseOrder), po.PurchaseOrderId, AuditActions.Update, before,
                new { po.PaymentStatus, po.Note, Lines = Snapshot(po) }));
            await _notifier.NotifyRoleAsync(RoleCodes.Accountant, new NotificationMessage(NotificationSources.Warehouse,
                Messages.NotifyPoRepricedTitle, string.Format(Messages.NotifyPoRepricedBody, po.PoCode),
                string.Format(PaymentLink, po.PurchaseOrderId), nameof(PurchaseOrder), po.PurchaseOrderId), ct);
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.PurchaseOrderRepriced);
        });
    }

    // ---------- Kế toán ----------

    public async Task<Result> ConfirmPaymentAsync(int purchaseOrderId, ConfirmPaymentDto dto, CancellationToken ct)
    {
        var po = await PurchaseOrders.Include(p => p.PurchaseOrderLines).Include(p => p.PurchasePayments)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct);
        if (po is null) return Fail.NotFound();
        if (!CanPay(po.GoodsStatus, po.PaymentStatus))
            return Result.Failure(ErrorCodes.PurchaseOrderInvalidStatus, Messages.PurchaseOrderInvalidStatus);

        var method = CodeText.ToCode(dto.PaymentMethod);
        if (!PaymentMethods.All.Contains(method))
            return Result.Failure(ErrorCodes.PaymentInvalid, Messages.PaymentMethodInvalid, ResultKind.Validation);
        if (dto.PaymentDate > _clock.Today)
            return Result.Failure(ErrorCodes.PaymentInvalid, Messages.PaymentDateFuture, ResultKind.Validation);
        // DEV-ALL-129: đơn thường so với tổng giá trị đơn; đơn Chốt không bù so với tổng giá trị thực tế Kế toán nhập.
        var payable = PayableAmount(po, dto);
        if (!payable.IsSuccess) return payable;
        var target = payable.Data;

        // DEV-ALL-189: mỗi lần thanh toán phải có ít nhất một ảnh chứng từ tải lên SAU lần thanh toán trước (lần đầu: bất kỳ ảnh nào).
        var lastPaymentAt = po.PurchasePayments.Select(p => (DateTime?)p.CreatedAt).Max();
        var newProofs = await _uow.Repository<Attachment>().Query().AsNoTracking().CountAsync(a =>
            a.OwnerType == AttachmentOwnerTypes.PurchaseOrder && a.OwnerId == po.PurchaseOrderId
            && a.Purpose == AttachmentPurposes.PaymentProof && a.IsCurrent
            && (lastPaymentAt == null || a.UploadedAt > lastPaymentAt), ct);
        if (newProofs == 0)
            return Result.Failure(ErrorCodes.PaymentInvalid, Messages.PaymentProofRequired, ResultKind.Validation);

        _uow.Repository<PurchaseOrder>().SetOriginalRowVersion(po, dto.RowVer);
        var from = po.PaymentStatus;
        // Mỗi lần trả là một dòng; tổng đã trả đủ mốc (giá trị đơn / giá trị thực tế) thì đơn thành đã thanh toán (DEV-ALL-136, 129).
        po.PurchasePayments.Add(new PurchasePayment
        {
            PaymentDate = dto.PaymentDate, Amount = dto.PaidAmount, PaymentMethod = method,
            PaymentNote = Clean(dto.PaymentNote), CreatedAt = _clock.Now,
        });
        PurchaseAmounts.Sync(po);

        return await _uow.InTransactionAsync(async () =>
        {
            _audit.Add(new AuditEntry(nameof(PurchaseOrder), po.PurchaseOrderId, AuditActions.Confirm,
                new { PaymentStatus = from },
                new { po.PaymentStatus, PaymentDate = dto.PaymentDate, Amount = dto.PaidAmount, po.PaidAmount, po.ActualAmount, po.PaymentMethod, po.PaymentNote }));
            await _notifier.NotifyRoleAsync(RoleCodes.Manager, new NotificationMessage(NotificationSources.Warehouse,
                Messages.NotifyPoPaidTitle, string.Format(Messages.NotifyPoPaidBody, po.PoCode),
                string.Format(ManagerLink, po.PurchaseOrderId), nameof(PurchaseOrder), po.PurchaseOrderId), ct);
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.PaymentConfirmed);
        });
    }

    /// <summary>
    /// DEV-ALL-129: số tiền làm mốc "đã thanh toán đủ". Đơn thường = tổng giá trị đơn. Đơn Chốt không bù = giá trị thực tế Kế
    /// toán đã nhập riêng (DEV-ALL-185; không vượt tổng giá trị đơn, không nhỏ hơn số đã trả).
    /// Số trả lần này không được vượt phần còn phải trả của mốc (DEV-ALL-136: đơn trả nhiều lần).
    /// </summary>
    private static Result<decimal> PayableAmount(PurchaseOrder po, ConfirmPaymentDto dto)
    {
        static Result<decimal> Invalid(string message) =>
            Result<decimal>.Failure(ErrorCodes.PaymentInvalid, message, ResultKind.Validation);

        if (dto.PaidAmount <= 0) return Invalid(Messages.PaymentAmountInvalid);
        var orderTotal = po.PurchaseOrderLines.Sum(l => l.OrderedQty * l.UnitPrice);
        var paid = PurchaseAmounts.Paid(po);
        if (po.GoodsStatus != PurchaseGoodsStatuses.ClosedShort)
            return dto.PaidAmount > orderTotal - paid ? Invalid(Messages.PaymentAmountExceedsTotal) : Result<decimal>.Success(orderTotal);

        // DEV-ALL-185: giá trị thực tế nhập riêng (nút riêng) trước khi thanh toán; ở đây chỉ đọc lại số đã lưu.
        if (po.ActualAmount is not { } actual || actual <= 0 || po.ActualAmountSetAt is null) return Invalid(Messages.PaymentActualAmountRequired);
        if (dto.PaidAmount > actual - paid) return Invalid(Messages.PaymentAmountExceedsActual);
        return Result<decimal>.Success(actual);
    }

    /// <summary>
    /// DEV-ALL-185: nhập giá trị thực tế của đơn Chốt không bù ở nút riêng. Nhập một lần; sau đó chỉ sửa được trong 24 giờ kể từ lần
    /// nhập đầu tiên, bắt buộc lý do. Không vượt tổng giá trị đơn, không nhỏ hơn số đã trả; trạng thái thanh toán tính lại theo số mới.
    /// </summary>
    public async Task<Result> SetActualAmountAsync(int purchaseOrderId, SetActualAmountDto dto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var po = await PurchaseOrders.Include(p => p.PurchaseOrderLines).Include(p => p.PurchasePayments)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct);
        if (po is null) return Fail.NotFound();
        if (po.GoodsStatus != PurchaseGoodsStatuses.ClosedShort)
            return Result.Failure(ErrorCodes.PurchaseOrderInvalidStatus, Messages.PurchaseOrderInvalidStatus);

        var now = _clock.Now;
        var entered = po.ActualAmountSetAt is not null;
        if (!CanSetActualAmount(po.GoodsStatus, po.ActualAmountSetAt, now))
            return Result.Failure(ErrorCodes.PaymentEditExpired, Messages.ActualAmountEditExpired);

        static Result Invalid(string message) => Result.Failure(ErrorCodes.PaymentInvalid, message, ResultKind.Validation);
        var reason = Clean(dto.Reason);
        if (entered && reason is null) return Invalid(Messages.ActualAmountEditReasonRequired);
        if (dto.Amount <= 0) return Invalid(Messages.PaymentActualAmountRequired);
        if (dto.Amount > po.PurchaseOrderLines.Sum(l => l.OrderedQty * l.UnitPrice)) return Invalid(Messages.PaymentActualAmountExceedsTotal);
        if (dto.Amount < PurchaseAmounts.Paid(po)) return Invalid(Messages.PaymentActualBelowPaid);

        _uow.Repository<PurchaseOrder>().SetOriginalRowVersion(po, dto.RowVer);
        var before = new { po.ActualAmount, po.PaymentStatus };
        po.ActualAmount = dto.Amount;
        po.ActualAmountSetAt ??= now;
        PurchaseAmounts.Sync(po);

        return await _uow.InTransactionAsync(async () =>
        {
            _audit.Add(new AuditEntry(nameof(PurchaseOrder), po.PurchaseOrderId, AuditActions.Update, before,
                new { po.ActualAmount, po.PaymentStatus }, reason));
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.ActualAmountSaved);
        });
    }

    /// <summary>Nhập được khi đơn Chốt không bù chưa nhập giá trị thực tế, hoặc trong 24 giờ kể từ lần nhập đầu tiên.</summary>
    internal static bool CanSetActualAmount(string goodsStatus, DateTime? setAt, DateTime now) =>
        goodsStatus == PurchaseGoodsStatuses.ClosedShort
        && (setAt is null || now - setAt.Value <= TimeSpan.FromHours(PurchasePaymentRules.EditWindowHours));

    public async Task<Result> ReturnPaymentAsync(int purchaseOrderId, ReturnPaymentDto dto, CancellationToken ct)
    {
        var po = await _uow.Repository<PurchaseOrder>().GetByIdAsync(purchaseOrderId);
        if (po is null) return Fail.NotFound();
        if (!CanReturn(po.GoodsStatus, po.PaymentStatus))
            return Result.Failure(ErrorCodes.PurchaseOrderInvalidStatus, Messages.PurchaseOrderInvalidStatus);

        var reason = dto.Reason.Trim();
        _uow.Repository<PurchaseOrder>().SetOriginalRowVersion(po, dto.RowVer);
        var from = po.PaymentStatus;
        po.PaymentStatus = PurchasePaymentStatuses.Returned;
        po.PaymentReturnReason = reason;   // CK_PO_PayReturn bắt buộc có lý do

        return await _uow.InTransactionAsync(async () =>
        {
            _audit.Add(new AuditEntry(nameof(PurchaseOrder), po.PurchaseOrderId, AuditActions.Reject,
                new { PaymentStatus = from }, new { po.PaymentStatus }, reason));
            await _notifier.NotifyRoleAsync(RoleCodes.Manager, new NotificationMessage(NotificationSources.Warehouse,
                Messages.NotifyPoReturnedTitle, string.Format(Messages.NotifyPoReturnedBody, po.PoCode, reason),
                string.Format(ManagerLink, po.PurchaseOrderId), nameof(PurchaseOrder), po.PurchaseOrderId), ct);
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.PaymentReturned);
        });
    }

    // ---------- Dùng chung ----------

    private Task<PurchaseOrder?> LoadWithLinesAsync(int purchaseOrderId, CancellationToken ct) =>
        PurchaseOrders.Include(p => p.PurchaseOrderLines).FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct);

    /// <summary>Danh sách dòng gửi lên phải khớp đúng tập dòng của đơn (không thiếu, không thừa, không trùng).</summary>
    private static bool SameLines(PurchaseOrder po, IEnumerable<int> lineIds)
    {
        var sent = lineIds.ToList();
        return sent.Count == po.PurchaseOrderLines.Count && sent.Distinct().Count() == sent.Count
            && sent.All(id => po.PurchaseOrderLines.Any(l => l.PurchaseOrderLineId == id));
    }

    private static object Snapshot(PurchaseOrder po) =>
        po.PurchaseOrderLines.OrderBy(l => l.PurchaseOrderLineId)
            .Select(l => new { l.PurchaseOrderLineId, l.OrderedQty, l.UnitPrice }).ToList();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Đơn đã đặt hàng, chưa trả đủ: Kế toán thêm được lần thanh toán (nhiều lần, DEV-ALL-136).</summary>
    internal static bool CanPay(string goodsStatus, string paymentStatus) =>
        PurchaseGoodsStatuses.Placed.Contains(goodsStatus)
        && paymentStatus is PurchaseOrderStatuses.Unpaid or PurchasePaymentStatuses.PartiallyPaid;

    /// <summary>Chỉ đơn đã đặt hàng và chưa trả lần nào mới trả lại Quản đốc được.</summary>
    internal static bool CanReturn(string goodsStatus, string paymentStatus) =>
        PurchaseGoodsStatuses.Placed.Contains(goodsStatus) && paymentStatus == PurchaseOrderStatuses.Unpaid;

    private async Task<IReadOnlyList<PurchasePaymentDto>> PaymentsOfAsync(int purchaseOrderId, string goodsStatus, CancellationToken ct)
    {
        var canEdit = _user.Role == Roles.Accountant && PurchaseGoodsStatuses.Placed.Contains(goodsStatus);
        var now = _clock.Now;
        var rows = await _uow.Repository<PurchasePayment>().Query().AsNoTracking()
            .Where(p => p.PurchaseOrderId == purchaseOrderId).OrderBy(p => p.CreatedAt).ThenBy(p => p.PurchasePaymentId).ToListAsync(ct);
        return rows.Select(p => new PurchasePaymentDto(p.PurchasePaymentId, p.PaymentDate, p.Amount, CodeText.ToPascal(p.PaymentMethod),
            p.PaymentNote, p.CreatedAt, p.UpdatedAt, p.EditReason, canEdit && PurchasePaymentService.IsEditable(p, now))).ToList();
    }

    private PurchaseOrderPermissionsDto PermissionsFor(string goodsStatus, string paymentStatus, DateTime? actualAmountSetAt, bool canEditOrder)
    {
        var isManager = _user.Role is Roles.Manager or Roles.Admin;
        var isAccountant = _user.Role == Roles.Accountant;
        var pending = goodsStatus == PurchaseOrderStatuses.PendingFinalisation;
        var payable = CanPay(goodsStatus, paymentStatus);
        var returnable = CanReturn(goodsStatus, paymentStatus);
        var repriceable = PurchaseGoodsStatuses.Repriceable.Contains(goodsStatus)
                          && paymentStatus == PurchasePaymentStatuses.Returned;
        return new PurchaseOrderPermissionsDto(isManager && pending, isManager && pending, isManager && repriceable,
            isAccountant && payable, isAccountant && returnable,
            isAccountant && CanSetActualAmount(goodsStatus, actualAmountSetAt, _clock.Now),
            isManager && canEditOrder);
    }
}
