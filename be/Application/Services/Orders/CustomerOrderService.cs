using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Orders;

public sealed class CustomerOrderService : ICustomerOrderService
{
    private readonly ICustomerOrderRepository _orders;
    private readonly IProductRepository _products;
    private readonly IGenericRepository<OrderLine> _lines;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IAuditLogService _audit;
    private readonly INotificationService _notifier;

    public CustomerOrderService(ICustomerOrderRepository orders, IProductRepository products,
        IGenericRepository<OrderLine> lines, IUnitOfWork uow, IClock clock, IAuditLogService audit,
        INotificationService notifier)
    {
        _orders = orders; _products = products; _lines = lines; _uow = uow; _clock = clock; _audit = audit; _notifier = notifier;
    }

    public async Task<Result<PagedResult<OrderListItemDto>>> ListAsync(OrderQuery query, CancellationToken ct)
    {
        string? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            status = CodeText.ToCode(query.Status);
            if (!IsKnownStatus(status))
                return Result<PagedResult<OrderListItemDto>>.Failure(ErrorCodes.CommonValidation, "Dữ liệu không hợp lệ.",
                    ResultKind.Validation, new Dictionary<string, string[]> { ["status"] = new[] { "Giá trị không hợp lệ." } });
        }
        return Result<PagedResult<OrderListItemDto>>.Success(await _orders.ListAsync(query, status, ct));
    }

    public async Task<Result<string>> SuggestCodeAsync(CancellationToken ct)
    {
        var prefix = $"DH{_clock.Now:yyMM}-";
        var max = await _orders.GetMaxCodeAsync(prefix, ct);
        var next = max is { Length: > 0 } && max.Length > prefix.Length && int.TryParse(max[prefix.Length..], out var n) ? n + 1 : 1;
        return Result<string>.Success($"{prefix}{next:000}");
    }

    public async Task<Result<OrderDetailDto>> GetAsync(int orderId, CancellationToken ct)
    {
        var detail = await _orders.GetDetailAsync(orderId, ct);
        if (detail is null) return Fail.NotFound<OrderDetailDto>();
        var history = await _audit.GetHistoryAsync(nameof(CustomerOrder), orderId, ct);
        return Result<OrderDetailDto>.Success(detail with { History = history });
    }

    public async Task<Result<int>> CreateAsync(CreateOrderDto dto, CancellationToken ct)
    {
        var code = dto.OrderCode.Trim().ToUpperInvariant();
        if (await _orders.CodeExistsAsync(code, null, ct))
            return Result<int>.Failure(ErrorCodes.OrderCodeDuplicate, Messages.OrderCodeDuplicate);
        var check = await CheckReferencesAsync(dto.CustomerId, dto.Lines.Select(l => l.ProductId), ct);
        if (!check.IsSuccess) return Fail.As<int>(check);
        var variants = await ResolveVariantsAsync(dto.Lines, ct);
        if (!variants.IsSuccess) return Fail.As<int>(variants);

        var order = new CustomerOrder
        {
            OrderCode = code,
            CustomerId = dto.CustomerId,
            ReceivedDate = dto.ReceivedDate,
            DueDate = dto.DueDate,
            Note = Clean(dto.Note),
            Status = OrderStatuses.New,
            MaterialStatus = OrderMaterialStatuses.Awaiting,
            CreatedAt = _clock.Now,
        };
        foreach (var l in dto.Lines) order.OrderLines.Add(NewLine(l, variants.Data!));

        return await _uow.InTransactionAsync(async () =>   // D17: log cần OrderId
        {
            await _orders.AddAsync(order);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(CustomerOrder), order.OrderId, AuditActions.Insert, null, Snapshot(order)));
            await _notifier.NotifyRoleAsync(RoleCodes.Manager, new NotificationMessage(NotificationSources.System,
                Messages.NotifyNewOrderTitle,
                string.Format(Messages.NotifyNewOrderBody, order.OrderCode, order.DueDate.ToString("dd/MM/yyyy")),
                $"/warehouse/order-materials?orderId={order.OrderId}", nameof(CustomerOrder), order.OrderId), ct);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(order.OrderId, Messages.Saved);
        });
    }

    public async Task<Result> UpdateAsync(int orderId, UpdateOrderDto dto, CancellationToken ct)
    {
        var order = await _orders.GetWithLinesAsync(orderId, ct);
        if (order is null) return Fail.NotFound();
        var isNew = order.Status == OrderStatuses.New;
        if (!isNew && !OrderStatuses.LimitedEdit.Contains(order.Status))
            return Result.Failure(ErrorCodes.OrderNotEditable, Messages.OrderNotEditable);

        var header = await CheckHeaderAsync(order, dto, isNew, ct);
        if (!header.IsSuccess) return header;
        var refs = await CheckReferencesAsync(dto.CustomerId, NewProductIds(order, dto.Lines), ct);
        if (!refs.IsSuccess) return refs;
        var variants = await ResolveVariantsAsync(dto.Lines, ct);
        if (!variants.IsSuccess) return variants;

        _orders.SetOriginalRowVersion(order, dto.RowVer);
        var before = Snapshot(order);
        var diff = ApplyLines(order, dto.Lines, isNew, variants.Data!);
        if (!diff.IsSuccess) return diff;

        order.OrderCode = dto.OrderCode.Trim().ToUpperInvariant();
        order.CustomerId = dto.CustomerId;
        order.ReceivedDate = dto.ReceivedDate;
        order.DueDate = dto.DueDate;
        order.Note = Clean(dto.Note);
        order.UpdatedAt = _clock.Now;
        _audit.Add(new AuditEntry(nameof(CustomerOrder), orderId, AuditActions.Update, before, Snapshot(order)));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    public async Task<Result> CancelAsync(int orderId, CancelOrderDto dto, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(orderId);
        if (order is null) return Fail.NotFound();
        if (order.Status != OrderStatuses.New)
            return Result.Failure(ErrorCodes.OrderInvalidStatus, Messages.OrderInvalidStatus);
        return await ChangeStatusAsync(order, dto.RowVer, OrderStatuses.Cancelled, dto.Reason, null, Messages.OrderCancelled);
    }

    public async Task<Result> StopAsync(int orderId, StopOrderDto dto, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(orderId);
        if (order is null) return Fail.NotFound();
        if (!OrderStatuses.Stoppable.Contains(order.Status))
            return Result.Failure(ErrorCodes.OrderInvalidStatus, Messages.OrderInvalidStatus);
        return await ChangeStatusAsync(order, dto.RowVer, OrderStatuses.Stopped, dto.Reason, dto.Reason.Trim(), Messages.OrderStopped);
    }

    public async Task<Result> StartProductionAsync(int orderId, StartOrderProductionDto dto, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(orderId);
        if (order is null) return Fail.NotFound();
        if (order.Status != OrderStatuses.New)
            return Result.Failure(ErrorCodes.OrderInvalidStatus, Messages.OrderInvalidStatus);
        if (order.MaterialStatus == OrderMaterialStatuses.Awaiting)
            return Result.Failure(ErrorCodes.OrderMaterialsAwaiting, Messages.OrderMaterialsAwaiting);
        return await ChangeStatusAsync(order, dto.RowVer, OrderStatuses.InProduction, null, null, Messages.OrderStarted);
    }

    private async Task<Result> ChangeStatusAsync(CustomerOrder order, byte[] rowVer, string to, string? reason,
        string? stopReason, string successMessage)
    {
        _orders.SetOriginalRowVersion(order, rowVer);
        var from = order.Status;
        order.Status = to;
        if (stopReason is not null) order.StopReason = stopReason;
        order.UpdatedAt = _clock.Now;
        _audit.Add(new AuditEntry(nameof(CustomerOrder), order.OrderId, AuditActions.Update, new { Status = from },
            new { Status = to }, Clean(reason)));
        await _uow.SaveChangesAsync();
        return Result.Success(successMessage);
    }

    /// <summary>Không phải NEW: mã đơn, khách, ngày nhận phải giữ nguyên. Đổi mã khi NEW: kiểm trùng.</summary>
    private async Task<Result> CheckHeaderAsync(CustomerOrder order, UpdateOrderDto dto, bool isNew, CancellationToken ct)
    {
        var code = dto.OrderCode.Trim().ToUpperInvariant();
        if (!isNew)
        {
            return code != order.OrderCode || dto.CustomerId != order.CustomerId || dto.ReceivedDate != order.ReceivedDate
                ? Result.Failure(ErrorCodes.OrderNotEditable, Messages.OrderHeaderLocked)
                : Result.Success();
        }
        return code != order.OrderCode && await _orders.CodeExistsAsync(code, order.OrderId, ct)
            ? Result.Failure(ErrorCodes.OrderCodeDuplicate, Messages.OrderCodeDuplicate)
            : Result.Success();
    }

    /// <summary>Khách phải tồn tại; mã hàng phải đang dùng (chỉ kiểm mã hàng của dòng mới / dòng đổi mã hàng).</summary>
    private async Task<Result> CheckReferencesAsync(int customerId, IEnumerable<int> productIds, CancellationToken ct)
    {
        if (!await _orders.CustomerExistsAsync(customerId, ct))
            return Result.Failure(ErrorCodes.CommonNotFound, Messages.CustomerNotFound);
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0) return Result.Success();
        var bad = await _products.FindInactiveCodesAsync(ids, ct);
        return bad.Count == 0
            ? Result.Success()
            : Result.Failure(ErrorCodes.ProductNotActive, string.Format(Messages.ProductNotActive, string.Join(", ", bad)));
    }

    private static IEnumerable<int> NewProductIds(CustomerOrder order, IReadOnlyList<OrderLineInput> inputs)
    {
        var old = order.OrderLines.ToDictionary(l => l.OrderLineId);
        return inputs.Where(i => i.OrderLineId is null || !old.TryGetValue(i.OrderLineId.Value, out var o) || o.ProductId != i.ProductId)
            .Select(i => i.ProductId);
    }

    /// <summary>D27. Dòng mới trùng khóa với dòng cũ sắp xóa (khi NEW) thì dùng lại dòng cũ, tránh INSERT trước DELETE.</summary>
    private Result ApplyLines(CustomerOrder order, IReadOnlyList<OrderLineInput> inputs, bool isNew,
        IReadOnlyDictionary<VariantKey, ProductVariant> variants)
    {
        var existing = order.OrderLines.ToDictionary(l => l.OrderLineId);
        if (inputs.Any(i => i.OrderLineId is int id && !existing.ContainsKey(id))) return Fail.NotFound();

        var keptIds = inputs.Where(i => i.OrderLineId is not null).Select(i => i.OrderLineId!.Value).ToHashSet();
        var removed = existing.Values.Where(l => !keptIds.Contains(l.OrderLineId)).ToList();
        if (!isNew && removed.Count > 0)
            return Result.Failure(ErrorCodes.OrderLineRemoveNotAllowed, Messages.OrderLineRemoveNotAllowed);

        var toAdd = new List<OrderLineInput>();
        foreach (var input in inputs)
        {
            if (input.OrderLineId is int id)
            {
                var line = existing[id];
                if (!isNew && KeyChanged(line, input))
                    return Result.Failure(ErrorCodes.OrderLineKeyLocked, Messages.OrderLineKeyLocked);
                Update(line, input, variants);
                continue;
            }
            var reusable = isNew ? removed.FirstOrDefault(l => SameKey(l, input)) : null;
            if (reusable is null) { toAdd.Add(input); continue; }
            removed.Remove(reusable);
            Update(reusable, input, variants);
        }

        foreach (var line in removed)
        {
            order.OrderLines.Remove(line);
            _lines.Remove(line);
        }
        foreach (var input in toAdd) order.OrderLines.Add(NewLine(input, variants));
        return Result.Success();
    }

    private readonly record struct VariantKey(int ProductId, string Color, string SizeCode);

    private static VariantKey KeyOf(int productId, string color, string sizeCode) =>
        new(productId, color.Trim().ToUpperInvariant(), sizeCode.Trim().ToUpperInvariant());

    /// <summary>
    /// Dòng đơn trỏ <c>ProductVariantId</c> (màu × size của mã hàng, FK ghép với ProductId). Màu × size chưa khai báo thì tạo
    /// biến thể mới (ACTIVE) để Admin lưu đơn như cũ; định mức của biến thể mới khai ở màn Mã hàng.
    /// </summary>
    private async Task<Result<IReadOnlyDictionary<VariantKey, ProductVariant>>> ResolveVariantsAsync(
        IReadOnlyList<OrderLineInput> inputs, CancellationToken ct)
    {
        var productIds = inputs.Select(i => i.ProductId).Distinct().ToArray();
        var existing = await _uow.Repository<ProductVariant>().Query().Where(v => productIds.Contains(v.ProductId)).ToListAsync(ct);
        var map = existing.ToDictionary(v => KeyOf(v.ProductId, v.Color, v.SizeCode));
        foreach (var input in inputs)
        {
            var key = KeyOf(input.ProductId, input.Color, input.SizeCode);
            if (map.TryGetValue(key, out var found))
            {
                if (found.Status != RecordStatus.Active)
                    return Result<IReadOnlyDictionary<VariantKey, ProductVariant>>.Failure(ErrorCodes.ProductNotActive,
                        string.Format(Messages.ProductVariantInactive, $"{input.Color.Trim()} – {input.SizeCode.Trim().ToUpperInvariant()}", input.ProductId));
                continue;
            }
            var created = new ProductVariant
            {
                ProductId = input.ProductId, Color = input.Color.Trim(), SizeCode = input.SizeCode.Trim().ToUpperInvariant(),
                Status = RecordStatus.Active,
            };
            await _uow.Repository<ProductVariant>().AddAsync(created);
            map[key] = created;
        }
        return Result<IReadOnlyDictionary<VariantKey, ProductVariant>>.Success(map);
    }

    private static bool SameKey(OrderLine l, OrderLineInput i) =>
        l.ProductId == i.ProductId
        && string.Equals(l.ProductVariant.Color, i.Color.Trim(), StringComparison.OrdinalIgnoreCase)
        && string.Equals(l.ProductVariant.SizeCode, i.SizeCode.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool KeyChanged(OrderLine l, OrderLineInput i) => !SameKey(l, i);

    private static void Update(OrderLine line, OrderLineInput input, IReadOnlyDictionary<VariantKey, ProductVariant> variants)
    {
        line.ProductId = input.ProductId;
        line.ProductVariant = variants[KeyOf(input.ProductId, input.Color, input.SizeCode)];
        line.OrderedQty = input.OrderedQty;
        line.CmtUnitPrice = input.CmtUnitPrice;
    }

    private static OrderLine NewLine(OrderLineInput i, IReadOnlyDictionary<VariantKey, ProductVariant> variants) => new()
    {
        ProductId = i.ProductId,
        ProductVariant = variants[KeyOf(i.ProductId, i.Color, i.SizeCode)],
        OrderedQty = i.OrderedQty,
        CmtUnitPrice = i.CmtUnitPrice,
    };

    private static bool IsKnownStatus(string code) =>
        code is OrderStatuses.New or OrderStatuses.InProduction or OrderStatuses.InDelivery or OrderStatuses.PartiallyDelivered
            or OrderStatuses.Delivered or OrderStatuses.Completed or OrderStatuses.Cancelled or OrderStatuses.Stopped;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Có đơn giá CMT: lịch sử đơn hàng chỉ Admin xem (D26).</summary>
    private static object Snapshot(CustomerOrder o) => new
    {
        o.OrderCode, o.CustomerId, o.ReceivedDate, o.DueDate, o.Note, o.Status, o.StopReason,
        Lines = o.OrderLines.OrderBy(l => l.ProductId).ThenBy(l => l.ProductVariant.Color).ThenBy(l => l.ProductVariant.SizeCode)
            .Select(l => new { l.OrderLineId, l.ProductId, l.ProductVariant.Color, l.ProductVariant.SizeCode, l.OrderedQty, l.CmtUnitPrice }).ToList(),
    };
}
