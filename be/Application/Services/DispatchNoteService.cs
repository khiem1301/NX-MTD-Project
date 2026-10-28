using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC45 / UC46 / UC47: phiếu xuất hàng kiêm lệnh giao. Quản đốc lập (thành phẩm được giữ chỗ) → Thủ kho lập phiếu xuất
/// (chốt số xuất) → Quản đốc duyệt hoặc trả lại → Thủ kho in, chụp ảnh và xuất (IN_DELIVERY, trừ kho FINISHED); Thủ kho
/// còn từ chối có lý do khi không đủ hàng, Kế toán xác nhận khách đã nhận → DELIVERED / PARTIALLY_DELIVERED.
/// Đơn hàng DELIVERED khi tổng khách đã nhận ≥ số đặt (GBR-23). Giao hàng luôn thành công (GBR-24).
/// </summary>
public sealed class DispatchNoteService : IDispatchNoteService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly IDocumentCodeGenerator _codes;
    private readonly INotificationService _notifier;
    private readonly IAuditLogService _audit;
    private readonly IAppLock _locks;
    private readonly IClock _clock;
    private readonly ILogger<DispatchNoteService> _logger;

    public DispatchNoteService(IUnitOfWork uow, IStockService stock, IDocumentCodeGenerator codes,
        INotificationService notifier, IAuditLogService audit, IAppLock locks, IClock clock,
        ILogger<DispatchNoteService> logger)
    {
        _uow = uow; _stock = stock; _codes = codes; _notifier = notifier; _audit = audit; _locks = locks;
        _clock = clock; _logger = logger;
    }

    private IQueryable<DispatchNote> Notes => _uow.Repository<DispatchNote>().Query();

    public async Task<Result<PagedResult<DispatchNoteListItemDto>>> GetPagedAsync(DispatchNoteQuery query,
        CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Notes.AsNoTracking()
            .Where(d => string.IsNullOrEmpty(query.Status) || d.Status == query.Status)
            .Where(d => query.OrderId == null || d.OrderId == query.OrderId)
            .Where(d => string.IsNullOrEmpty(keyword) || d.DispatchCode.Contains(keyword)
                        || d.Order.OrderCode.Contains(keyword) || d.Order.Customer.CustomerName.Contains(keyword))
            .OrderByDescending(d => d.DispatchNoteId)
            .Select(d => new DispatchNoteListItemDto(d.DispatchNoteId, d.DispatchCode, d.OrderId, d.Order.OrderCode,
                d.Order.Customer.CustomerName, d.DeliveryDate, d.Status, d.CreatedAt,
                d.DispatchNoteLines.Sum(l => l.IssueQty),
                d.CustomerReceivedDate == null ? (int?)null : d.DispatchNoteLines.Sum(l => l.ReceivedQty ?? 0)))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<DispatchNoteListItemDto>>.Success(page);
    }

    public async Task<Result<DispatchNoteDto>> GetByIdAsync(int dispatchNoteId, CancellationToken ct)
    {
        var dto = await Notes.AsNoTracking().Where(d => d.DispatchNoteId == dispatchNoteId)
            .Select(d => new DispatchNoteDto(d.DispatchNoteId, d.DispatchCode, d.OrderId, d.Order.OrderCode,
                d.Order.Customer.CustomerName, d.DeliveryDate, d.DeliveryAddress, d.Note, d.Status, d.CreatedAt,
                d.RejectReason, d.DispatchedByNavigation == null ? null : d.DispatchedByNavigation.FullName,
                d.DispatchedAt, d.CustomerReceivedDate,
                d.DispatchNoteLines.OrderBy(l => l.DispatchNoteLineId).Select(l => new DispatchNoteLineDto(
                    l.DispatchNoteLineId, l.OrderLineId, l.OrderLine.ProductVariant.Product.ProductCode, l.OrderLine.ProductVariant.Color,
                    l.OrderLine.ProductVariant.SizeCode, l.IssueQty, l.ReceivedQty)).ToList()))
            .FirstOrDefaultAsync(ct);
        return dto is null ? WarehouseFailures.NotFound<DispatchNoteDto>() : Result<DispatchNoteDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyList<DispatchableLineDto>>> GetDispatchableAsync(int orderId, CancellationToken ct)
    {
        var exists = await _uow.Repository<CustomerOrder>().Query().AnyAsync(o => o.OrderId == orderId, ct);
        if (!exists) return WarehouseFailures.NotFound<IReadOnlyList<DispatchableLineDto>>();
        return Result<IReadOnlyList<DispatchableLineDto>>.Success(await DispatchableAsync(orderId, ct));
    }

    public async Task<Result<DispatchNoteDto>> CreateAsync(CreateDispatchNoteDto dto, CancellationToken ct)
    {
        var created = await _uow.InTransactionAsync(async () =>
        {
            var order = await _uow.Repository<CustomerOrder>().Query().FirstOrDefaultAsync(o => o.OrderId == dto.OrderId, ct);
            if (order is null) return WarehouseFailures.NotFound<int>();
            if (OrderStatuses.Closed.Contains(order.Status))
                return Result<int>.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);

            await _locks.AcquireAsync($"dispatch:{order.OrderId}", ct);
            var lines = (await DispatchableAsync(order.OrderId, ct)).ToDictionary(l => l.OrderLineId);
            if (dto.Lines.Any(l => !lines.ContainsKey(l.OrderLineId)))
                return Result<int>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DispatchLineNotInOrder);
            var over = dto.Lines.Where(l => l.IssueQty > lines[l.OrderLineId].RemainingQty)
                .Select(l => $"{lines[l.OrderLineId].Color}-{lines[l.OrderLineId].SizeCode}: còn {lines[l.OrderLineId].RemainingQty}, xuất {l.IssueQty}")
                .ToList();
            if (over.Count > 0)
                return Result<int>.Failure(ErrorCodes.DispatchExceedsOrder,
                    $"{WarehouseMessages.DispatchExceedsOrder}: {string.Join("; ", over)}.");

            // Giữ chỗ thành phẩm: không xuất vượt số khả dụng (GBR-07, MSG13). vw_StockBalance tính phiếu PENDING là đã giữ.
            var enough = await _stock.EnsureAvailableAsync(
                dto.Lines.Select(l => new StockRequirement(StockKey.Finished(l.OrderLineId), l.IssueQty)).ToList(), ct);
            if (!enough.IsSuccess) return WarehouseFailures.From<int>(enough);

            var note = new DispatchNote
            {
                DispatchCode = await _codes.NextAsync<DispatchNote>(DocumentPrefixes.DispatchNote, d => d.DispatchCode, ct),
                OrderId = order.OrderId,
                DeliveryDate = dto.DeliveryDate,
                DeliveryAddress = dto.DeliveryAddress.Trim(),
                Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
                Status = DispatchStatuses.Pending,
                CreatedAt = _clock.Now,
                DispatchNoteLines = dto.Lines.Select(l => new DispatchNoteLine { OrderLineId = l.OrderLineId, IssueQty = l.IssueQty }).ToList(),
            };
            await _uow.Repository<DispatchNote>().AddAsync(note);
            await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
                "Có phiếu xuất hàng chờ lập phiếu xuất", $"Phiếu {note.DispatchCode} cho đơn {order.OrderCode}: Thủ kho vui lòng chốt số xuất và gửi Quản đốc duyệt.", "/warehouse/dispatch-notes"), ct);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(DispatchNote), note.DispatchNoteId, AuditActions.Insert, null,
                new { note.DispatchCode, note.Status, Qty = dto.Lines.Sum(l => l.IssueQty) }));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(note.DispatchNoteId, WarehouseMessages.DispatchCreated);
        });
        return created.IsSuccess ? await GetByIdAsync(created.Data, ct) : WarehouseFailures.From<DispatchNoteDto>(created);
    }

    public async Task<Result> PrepareAsync(int dispatchNoteId, PrepareDispatchDto dto, CancellationToken ct)
    {
        var note = await Notes.Include(d => d.DispatchNoteLines).Include(d => d.Order)
            .FirstOrDefaultAsync(d => d.DispatchNoteId == dispatchNoteId, ct);
        if (note is null) return WarehouseFailures.NotFound();
        if (note.Status != DispatchStatuses.Pending) return WarehouseFailures.InvalidStatus();

        var byId = dto.Lines.ToDictionary(l => l.DispatchNoteLineId);
        if (byId.Count != note.DispatchNoteLines.Count || note.DispatchNoteLines.Any(l => !byId.ContainsKey(l.DispatchNoteLineId)))
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.LinesRequired);
        if (note.DispatchNoteLines.Any(l => byId[l.DispatchNoteLineId].IssueQty < 1 || byId[l.DispatchNoteLineId].IssueQty > l.IssueQty))
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DispatchPrepareQtyInvalid);

        var before = note.DispatchNoteLines.Sum(l => l.IssueQty);
        foreach (var line in note.DispatchNoteLines) line.IssueQty = byId[line.DispatchNoteLineId].IssueQty;   // giảm số xuất thì nhả giữ chỗ
        note.Status = DispatchStatuses.PendingApproval;
        note.RejectReason = null;   // lý do Quản đốc trả lại lần trước (nếu có) đã được xử lý
        if (!string.IsNullOrWhiteSpace(dto.Note))
            note.Note = string.IsNullOrWhiteSpace(note.Note) ? dto.Note.Trim() : $"{note.Note}\n{dto.Note.Trim()}";
        await _notifier.NotifyRoleAsync(RoleCodes.Manager, new NotificationMessage(NotificationSources.Warehouse,
            "Phiếu xuất hàng chờ duyệt", $"Thủ kho đã lập phiếu xuất {note.DispatchCode} (đơn {note.Order.OrderCode}), vui lòng duyệt hoặc trả lại.",
            "/warehouse/dispatch-notes"), ct);
        _audit.Add(new AuditEntry(nameof(DispatchNote), note.DispatchNoteId, AuditActions.Update,
            new { Status = DispatchStatuses.Pending, Qty = before }, new { note.Status, Qty = note.DispatchNoteLines.Sum(l => l.IssueQty) }, "Lập phiếu xuất"));
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.DispatchPrepared);
    }

    public async Task<Result> ApproveAsync(int dispatchNoteId, CancellationToken ct)
    {
        var note = await Notes.Include(d => d.Order).FirstOrDefaultAsync(d => d.DispatchNoteId == dispatchNoteId, ct);
        if (note is null) return WarehouseFailures.NotFound();
        if (note.Status != DispatchStatuses.PendingApproval) return WarehouseFailures.InvalidStatus();

        note.Status = DispatchStatuses.Approved;
        await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
            "Phiếu xuất hàng đã duyệt", $"Quản đốc đã duyệt phiếu {note.DispatchCode}: vui lòng in, chụp ảnh phiếu có ký và xuất hàng.",
            "/warehouse/dispatch-notes"), ct);
        _audit.Add(new AuditEntry(nameof(DispatchNote), note.DispatchNoteId, AuditActions.Approve,
            new { Status = DispatchStatuses.PendingApproval }, new { note.Status }));
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.DispatchApproved);
    }

    public async Task<Result> ReturnAsync(int dispatchNoteId, ReasonDto dto, CancellationToken ct)
    {
        var note = await Notes.FirstOrDefaultAsync(d => d.DispatchNoteId == dispatchNoteId, ct);
        if (note is null) return WarehouseFailures.NotFound();
        if (note.Status != DispatchStatuses.PendingApproval) return WarehouseFailures.InvalidStatus();

        note.Status = DispatchStatuses.Pending;
        note.RejectReason = $"Quản đốc trả lại: {dto.Reason.Trim()}";
        await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
            "Phiếu xuất hàng bị trả lại", $"Quản đốc trả lại phiếu {note.DispatchCode}: {dto.Reason.Trim()}", "/warehouse/dispatch-notes"), ct);
        _audit.Add(new AuditEntry(nameof(DispatchNote), note.DispatchNoteId, AuditActions.Reject,
            new { Status = DispatchStatuses.PendingApproval }, new { note.Status }, dto.Reason.Trim()));
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.DispatchReturned);
    }

    public async Task<Result> CancelAsync(int dispatchNoteId, CancellationToken ct)
    {
        var note = await Notes.FirstOrDefaultAsync(d => d.DispatchNoteId == dispatchNoteId, ct);
        if (note is null) return WarehouseFailures.NotFound();
        if (!DispatchStatuses.NotYetExported.Contains(note.Status)) return WarehouseFailures.InvalidStatus();

        var from = note.Status;
        note.Status = DispatchStatuses.Cancelled;
        _audit.Add(new AuditEntry(nameof(DispatchNote), note.DispatchNoteId, AuditActions.Update,
            new { Status = from }, new { note.Status }));
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.DispatchCancelled);
    }

    public Task<Result> ExportAsync(int dispatchNoteId, int employeeId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var note = await Notes.Include(d => d.DispatchNoteLines).Include(d => d.Order)
                .FirstOrDefaultAsync(d => d.DispatchNoteId == dispatchNoteId, ct);
            if (note is null) return WarehouseFailures.NotFound();
            if (note.Status != DispatchStatuses.Approved) return WarehouseFailures.InvalidStatus();   // chỉ xuất sau khi Quản đốc duyệt
            // Swimlane: Thủ kho in, chụp ảnh phiếu xuất có Quản đốc ký rồi mới xuất.
            if (!await HasPhotoAsync(dispatchNoteId, AttachmentPurposes.SignedIssueSlip, ct))
                return Result.Failure(ErrorCodes.DispatchIssuePhotoRequired, WarehouseMessages.DispatchIssuePhotoRequired);

            var today = _clock.Today;
            var posted = await _stock.PostAsync(note.DispatchNoteLines
                .Select(l => new StockMovement(StockKey.Finished(l.OrderLineId), -l.IssueQty, StockDocTypes.DispatchNote,
                    note.DispatchNoteId, l.DispatchNoteLineId, today)).ToList(), ct);
            if (!posted.IsSuccess) return posted;

            note.Status = DispatchStatuses.InDelivery;
            note.DispatchedBy = employeeId;
            note.DispatchedAt = _clock.Now;
            if (OrderStatuses.Running.Contains(note.Order.Status)) note.Order.Status = OrderStatuses.InDelivery;
            await _notifier.NotifyRoleAsync(RoleCodes.Accountant, new NotificationMessage(NotificationSources.Warehouse,
                "Hàng đã xuất, chờ xác nhận khách nhận",
                $"Phiếu {note.DispatchCode} đã xuất kho; khi khách nhận hàng vui lòng xác nhận và chụp biên bản giao.", null), ct);
            _audit.Add(new AuditEntry(nameof(DispatchNote), note.DispatchNoteId, AuditActions.Confirm,
                new { Status = DispatchStatuses.Approved }, new { note.Status }));
            await _uow.SaveChangesAsync();
            _logger.LogInformation("Dispatch note {Code} exported by employee {EmployeeId}", note.DispatchCode, employeeId);
            return Result.Success(WarehouseMessages.DispatchExported);
        });

    public async Task<Result> RejectAsync(int dispatchNoteId, ReasonDto dto, int employeeId, CancellationToken ct)
    {
        var note = await Notes.Include(d => d.Order).FirstOrDefaultAsync(d => d.DispatchNoteId == dispatchNoteId, ct);
        if (note is null) return WarehouseFailures.NotFound();
        if (note.Status != DispatchStatuses.Pending) return WarehouseFailures.InvalidStatus();

        note.Status = DispatchStatuses.Rejected;
        note.RejectReason = dto.Reason.Trim();
        note.DispatchedBy = employeeId;
        note.DispatchedAt = _clock.Now;
        await _notifier.NotifyRoleAsync(RoleCodes.Manager, new NotificationMessage(NotificationSources.Warehouse,
            "Phiếu xuất hàng bị từ chối", $"Thủ kho từ chối phiếu {note.DispatchCode}: {note.RejectReason}", null), ct);
        _audit.Add(new AuditEntry(nameof(DispatchNote), note.DispatchNoteId, AuditActions.Reject,
            new { Status = DispatchStatuses.Pending }, new { note.Status }, note.RejectReason));
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.DispatchRejected);
    }

    public Task<Result> ConfirmDeliveryAsync(int dispatchNoteId, ConfirmDeliveryDto dto, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var note = await Notes.Include(d => d.DispatchNoteLines).Include(d => d.Order)
                .FirstOrDefaultAsync(d => d.DispatchNoteId == dispatchNoteId, ct);
            if (note is null) return WarehouseFailures.NotFound();
            if (note.Status != DispatchStatuses.InDelivery) return WarehouseFailures.InvalidStatus();
            // Biên bản giao hàng có khách ký là chứng từ xác nhận khách đã nhận.
            if (!await HasPhotoAsync(dispatchNoteId, AttachmentPurposes.SignedDeliveryNote, ct))
                return Result.Failure(ErrorCodes.DispatchDeliveryPhotoRequired, WarehouseMessages.DispatchDeliveryPhotoRequired);
            if (note.DispatchedAt is { } sent && dto.CustomerReceivedDate < DateOnly.FromDateTime(sent))
                return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DispatchReceivedDateInvalid);

            var byId = dto.Lines.ToDictionary(l => l.DispatchNoteLineId);
            if (byId.Count != note.DispatchNoteLines.Count || note.DispatchNoteLines.Any(l => !byId.ContainsKey(l.DispatchNoteLineId)))
                return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.LinesRequired);
            foreach (var line in note.DispatchNoteLines)
            {
                var received = byId[line.DispatchNoteLineId].ReceivedQty;
                if (received > line.IssueQty)
                    return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DispatchReceivedExceedsIssued);
                line.ReceivedQty = received;
            }

            var all = note.DispatchNoteLines.All(l => l.ReceivedQty == l.IssueQty);
            note.Status = all ? DispatchStatuses.Delivered : DispatchStatuses.PartiallyDelivered;
            note.CustomerReceivedDate = dto.CustomerReceivedDate;
            await _uow.SaveChangesAsync();   // lưu số khách nhận trước khi cộng dồn trạng thái đơn hàng

            await UpdateOrderDeliveryStatusAsync(note.Order, ct);
            _audit.Add(new AuditEntry(nameof(DispatchNote), note.DispatchNoteId, AuditActions.Confirm,
                new { Status = DispatchStatuses.InDelivery }, new { note.Status, note.CustomerReceivedDate }));
            await _uow.SaveChangesAsync();
            return Result.Success(WarehouseMessages.DispatchDelivered);
        });

    /// <summary>GBR-23: đơn DELIVERED khi tổng khách đã nhận ≥ số đặt; còn lại PARTIALLY_DELIVERED.</summary>
    private async Task UpdateOrderDeliveryStatusAsync(CustomerOrder order, CancellationToken ct)
    {
        if (OrderStatuses.Closed.Contains(order.Status)) return;
        var ordered = await _uow.Repository<OrderLine>().Query().Where(l => l.OrderId == order.OrderId).SumAsync(l => l.OrderedQty, ct);
        var delivered = await _uow.Repository<DispatchNoteLine>().Query()
            .Where(l => l.DispatchNote.OrderId == order.OrderId
                        && (l.DispatchNote.Status == DispatchStatuses.Delivered || l.DispatchNote.Status == DispatchStatuses.PartiallyDelivered))
            .SumAsync(l => l.ReceivedQty ?? 0, ct);
        order.Status = delivered >= ordered ? OrderStatuses.Delivered : OrderStatuses.PartiallyDelivered;
    }

    private Task<bool> HasPhotoAsync(int dispatchNoteId, string purpose, CancellationToken ct) =>
        _uow.Repository<Attachment>().Query().AnyAsync(a => a.OwnerType == AttachmentOwnerTypes.DispatchNote
            && a.OwnerId == dispatchNoteId && a.Purpose == purpose && a.IsCurrent, ct);

    private async Task<IReadOnlyList<DispatchableLineDto>> DispatchableAsync(int orderId, CancellationToken ct)
    {
        var lines = await _uow.Repository<OrderLine>().Query().AsNoTracking().Where(l => l.OrderId == orderId)
            .OrderBy(l => l.ProductVariant.Product.ProductCode).ThenBy(l => l.ProductVariant.Color).ThenBy(l => l.ProductVariant.SizeCode)
            .Select(l => new { l.OrderLineId, l.ProductVariant.Product.ProductCode, l.ProductVariant.Color, l.ProductVariant.SizeCode, l.OrderedQty })
            .ToListAsync(ct);
        var ids = lines.Select(l => l.OrderLineId).ToList();
        // Mảng (không phải IReadOnlySet) để EF dịch được thành IN (...).
        var issuing = DispatchStatuses.Issuing.ToArray();
        var issued = await _uow.Repository<DispatchNoteLine>().Query()
            .Where(l => ids.Contains(l.OrderLineId) && issuing.Contains(l.DispatchNote.Status))
            .GroupBy(l => l.OrderLineId).Select(g => new { g.Key, Qty = g.Sum(l => l.IssueQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        var result = new List<DispatchableLineDto>(lines.Count);
        foreach (var l in lines)
        {
            var done = issued.GetValueOrDefault(l.OrderLineId);
            var available = await _stock.GetAvailableAsync(StockKey.Finished(l.OrderLineId), ct);
            result.Add(new DispatchableLineDto(l.OrderLineId, l.ProductCode, l.Color, l.SizeCode, l.OrderedQty, done,
                Math.Max(0, l.OrderedQty - done), available));
        }
        return result;
    }
}

/// <summary>
/// Quyền ảnh phiếu xuất hàng (OwnerType = DISPATCH_NOTE): ảnh phiếu xuất có Quản đốc ký do Thủ kho chụp (SIGNED_ISSUE_SLIP) và
/// biên bản giao có khách ký do Kế toán chụp (SIGNED_DELIVERY_NOTE) — hai ảnh khác nhau, cùng chủ sở hữu là phiếu xuất.
/// </summary>
public sealed class DispatchNoteAttachmentAccess : IAttachmentOwnerAccess
{
    private const int MaxPhotos = 5;

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;

    public DispatchNoteAttachmentAccess(IUnitOfWork uow, ICurrentUser user)
    {
        _uow = uow; _user = user;
    }

    public string OwnerType => AttachmentOwnerTypes.DispatchNote;

    public async Task<bool> CanReadAsync(int ownerId, CancellationToken ct) =>
        _user.Role is Roles.WarehouseKeeper or Roles.Manager or Roles.Accountant or Roles.Admin
        && await GetStatusAsync(ownerId, ct) is not null;

    public async Task<bool> CanWriteAsync(int ownerId, CancellationToken ct)
    {
        var status = await GetStatusAsync(ownerId, ct);
        return _user.Role switch
        {
            // Thủ kho chụp phiếu xuất từ lúc xuất hàng; Kế toán chụp biên bản khi hàng đang giao hoặc vừa xác nhận.
            // Ảnh phiếu xuất có Quản đốc ký chụp sau khi duyệt, trước / lúc xuất hàng.
            Roles.WarehouseKeeper => status is DispatchStatuses.Approved or DispatchStatuses.InDelivery,
            Roles.Accountant => status is DispatchStatuses.InDelivery or DispatchStatuses.Delivered or DispatchStatuses.PartiallyDelivered,
            _ => false,
        };
    }

    public Task<int> MaxFilesAsync(CancellationToken ct) => Task.FromResult(MaxPhotos);

    private Task<string?> GetStatusAsync(int dispatchNoteId, CancellationToken ct) =>
        _uow.Repository<DispatchNote>().Query().AsNoTracking().Where(d => d.DispatchNoteId == dispatchNoteId)
            .Select(d => (string?)d.Status).FirstOrDefaultAsync(ct);
}
