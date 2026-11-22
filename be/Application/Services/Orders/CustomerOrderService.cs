using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

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

    public async Task<Result<PagedResult<OrderOverviewListItemDto>>> ListOverviewAsync(OrderQuery query, CancellationToken ct)
    {
        var list = await ListAsync(query, ct);
        if (!list.IsSuccess) return Fail.As<PagedResult<OrderOverviewListItemDto>>(list);
        var page = list.Data!;
        var items = page.Items.Select(o => new OrderOverviewListItemDto(o.OrderId, o.OrderCode, o.CustomerName, o.ReceivedDate,
            o.DueDate, o.Status, o.MaterialStatus, o.IsDelayRisk, o.TotalQty, o.ProductCodes)).ToList();
        return Result<PagedResult<OrderOverviewListItemDto>>.Success(
            new PagedResult<OrderOverviewListItemDto>(items, page.Page, page.PageSize, page.TotalItems));
    }

    public async Task<Result<OrderOverviewDto>> GetOverviewAsync(int orderId, CancellationToken ct)
    {
        var o = await _orders.GetDetailAsync(orderId, ct);
        if (o is null) return Fail.NotFound<OrderOverviewDto>();
        var lines = o.Lines.Select(l => new OrderOverviewLineDto(l.OrderLineId, l.ProductId, l.ProductCode,
            l.Color, l.SizeCode, l.OrderedQty, l.DeliveredQty)).ToList();
        return Result<OrderOverviewDto>.Success(new OrderOverviewDto(o.OrderId, o.OrderCode, o.CustomerName, o.ReceivedDate, o.DueDate,
            o.Status, o.MaterialStatus, o.IsDelayRisk, o.StopReason, o.DeliveryAddress, o.Note, o.CreatedAt, lines));
    }

    public async Task<Result<OrderSaveResultDto>> CreateAsync(CreateOrderDto dto, CancellationToken ct)
    {
        var input = ValidateOrderInput(dto);
        if (input is not null) return Fail.As<OrderSaveResultDto>(input);

        var code = dto.OrderCode.Trim().ToUpperInvariant();
        if (await _orders.CodeExistsAsync(code, null, ct))
            return Result<OrderSaveResultDto>.Failure(ErrorCodes.OrderCodeDuplicate, Messages.OrderCodeDuplicate);
        var check = await CheckReferencesAsync(dto.CustomerId, dto.Lines.Select(l => l.ProductId).Where(id => id > 0), ct);
        if (!check.IsSuccess) return Fail.As<OrderSaveResultDto>(check);

        return await _uow.InTransactionAsync(async () =>   // D17: log cần OrderId; mã hàng mới cùng giao dịch (DEV-ALL-151)
        {
            var created = await InsertStagedProductsAsync(dto.StagedProducts, dto.Lines, ct);
            if (!created.IsSuccess) return Fail.As<OrderSaveResultDto>(created);
            var bound = BindProductIds(dto.Lines, created.Data!);
            if (!bound.IsSuccess) return Fail.As<OrderSaveResultDto>(bound);
            var lines = bound.Data!;
            var variants = await ResolveVariantsAsync(lines, ct);
            if (!variants.IsSuccess) return Fail.As<OrderSaveResultDto>(variants);

            var order = new CustomerOrder
            {
                OrderCode = code,
                CustomerId = dto.CustomerId,
                ReceivedDate = dto.ReceivedDate,
                DueDate = dto.DueDate,
                DeliveryAddress = dto.DeliveryAddress.Trim(),
                Note = Clean(dto.Note),
                Status = OrderStatuses.New,
                MaterialStatus = OrderMaterialStatuses.Awaiting,
                CreatedAt = _clock.Now,
            };
            foreach (var l in lines) order.OrderLines.Add(NewLine(l, variants.Data!));

            await _orders.AddAsync(order);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(CustomerOrder), order.OrderId, AuditActions.Insert, null, Snapshot(order)));
            await _notifier.NotifyRoleAsync(RoleCodes.Manager, new NotificationMessage(NotificationSources.System,
                Messages.NotifyNewOrderTitle,
                string.Format(Messages.NotifyNewOrderBody, order.OrderCode, order.DueDate.ToString("dd/MM/yyyy")),
                $"/manager/orders/{order.OrderId}?tab=materials", nameof(CustomerOrder), order.OrderId), ct);
            await _uow.SaveChangesAsync();
            return Result<OrderSaveResultDto>.Success(new OrderSaveResultDto(order.OrderId, created.Data!), Messages.Saved);
        });
    }

    public async Task<Result<OrderSaveResultDto>> UpdateAsync(int orderId, UpdateOrderDto dto, CancellationToken ct)
    {
        var order = await _orders.GetWithLinesAsync(orderId, ct);
        if (order is null) return Fail.NotFound<OrderSaveResultDto>();

        var isNew = order.Status == OrderStatuses.New;
        if (!isNew && !OrderStatuses.LimitedEdit.Contains(order.Status))
            return Result<OrderSaveResultDto>.Failure(ErrorCodes.OrderNotEditable, Messages.OrderNotEditable);

        var input = ValidateOrderInput(dto);
        if (input is not null) return Fail.As<OrderSaveResultDto>(input);

        var header = await CheckHeaderAsync(order, dto, isNew, ct);
        if (!header.IsSuccess) return Fail.As<OrderSaveResultDto>(header);
        var refs = await CheckReferencesAsync(dto.CustomerId, NewProductIds(order, dto.Lines).Where(id => id > 0), ct, order.CustomerId);
        if (!refs.IsSuccess) return Fail.As<OrderSaveResultDto>(refs);

        if (dto.StagedProducts is not { Count: > 0 })
            return await ApplyUpdateAsync(order, dto, dto.Lines, [], ct);

        return await _uow.InTransactionAsync(async () =>
        {
            var created = await InsertStagedProductsAsync(dto.StagedProducts, dto.Lines, ct);
            if (!created.IsSuccess) return Fail.As<OrderSaveResultDto>(created);
            var bound = BindProductIds(dto.Lines, created.Data!);
            if (!bound.IsSuccess) return Fail.As<OrderSaveResultDto>(bound);
            return await ApplyUpdateAsync(order, dto, bound.Data!, created.Data!, ct);
        });
    }

    /// <summary>Ghi phần sửa đơn. Khi có mã hàng mới thì chạy trong transaction của UpdateAsync để lỗi đơn thì mã hàng cũng không còn.</summary>
    private async Task<Result<OrderSaveResultDto>> ApplyUpdateAsync(CustomerOrder order, UpdateOrderDto dto,
        IReadOnlyList<OrderLineInput> lines, IReadOnlyList<CreatedProductDto> created, CancellationToken ct)
    {
        var isNew = order.Status == OrderStatuses.New;
        var variants = await ResolveVariantsAsync(lines, ct);
        if (!variants.IsSuccess) return Fail.As<OrderSaveResultDto>(variants);
        var floor = await CheckOrderedQtyFloorAsync(lines, ct);
        if (floor is not null) return Fail.As<OrderSaveResultDto>(floor);

        _orders.SetOriginalRowVersion(order, dto.RowVer);
        if (RowVerMismatch(order.RowVer, dto.RowVer) is { } stale) return Fail.As<OrderSaveResultDto>(stale);
        var before = Snapshot(order);
        var diff = ApplyLines(order, lines, isNew, variants.Data!);
        if (!diff.IsSuccess) return Fail.As<OrderSaveResultDto>(diff);

        order.OrderCode = dto.OrderCode.Trim().ToUpperInvariant();
        order.CustomerId = dto.CustomerId;
        order.ReceivedDate = dto.ReceivedDate;
        order.DueDate = dto.DueDate;
        order.DeliveryAddress = dto.DeliveryAddress.Trim();
        order.Note = Clean(dto.Note);
        order.UpdatedAt = _clock.Now;
        _audit.Add(new AuditEntry(nameof(CustomerOrder), order.OrderId, AuditActions.Update, before, Snapshot(order)));
        await _uow.SaveChangesAsync();
        return Result<OrderSaveResultDto>.Success(new OrderSaveResultDto(order.OrderId, created), Messages.Saved);
    }

    /// <summary>
    /// DEV-ALL-151: tạo các mã hàng form đơn đang giữ, chỉ những mã có dòng. Trùng mã thì không ghi mã nào.
    /// Gọi trong transaction của đơn: SaveChanges lấy ProductId, commit do transaction bên ngoài.
    /// </summary>
    private async Task<Result<IReadOnlyList<CreatedProductDto>>> InsertStagedProductsAsync(
        IReadOnlyList<StagedProductDto>? staged, IReadOnlyList<OrderLineInput> lines, CancellationToken ct)
    {
        var used = lines.Select(l => l.ProductId).ToHashSet();
        if (staged is { Count: > 0 } && staged.Any(s => !used.Contains(s.TempId)))
            return Result<IReadOnlyList<CreatedProductDto>>.Failure(ErrorCodes.CommonValidation, Messages.StagedProductUnused,
                ResultKind.Validation);
        var items = (staged ?? []).Where(s => used.Contains(s.TempId)).ToList();
        if (items.Count == 0) return Result<IReadOnlyList<CreatedProductDto>>.Success([]);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<(StagedProductDto Item, Product Product, string Code)>();
        foreach (var item in items)
        {
            var code = item.ProductCode.Trim().ToUpperInvariant();
            if (!seen.Add(code) || await _products.CodeExistsAsync(code, null, ct))
                return Result<IReadOnlyList<CreatedProductDto>>.Failure(ErrorCodes.ProductCodeDuplicate, Messages.ProductCodeDuplicate);
            var product = new Product
            {
                ProductCode = code,
                SampleImageUrl = Clean(item.SampleImageUrl),
                Description = Clean(item.Description),
                Status = RecordStatus.Active,
                CreatedAt = _clock.Now,
            };
            foreach (var image in ExtraImages(item.ExtraImages)) product.ProductImages.Add(image);
            pending.Add((item, product, code));
        }
        foreach (var (_, product, _) in pending) await _products.AddAsync(product);
        await _uow.SaveChangesAsync();

        var created = new List<CreatedProductDto>(pending.Count);
        foreach (var (item, product, code) in pending)
        {
            _audit.Add(new AuditEntry(nameof(Product), product.ProductId, AuditActions.Insert, null,
                new { product.ProductCode, product.SampleImageUrl, product.Description, product.Status, ExtraImages = product.ProductImages.Count }));
            created.Add(new CreatedProductDto(item.TempId, product.ProductId, code));
        }
        return Result<IReadOnlyList<CreatedProductDto>>.Success(created);
    }

    /// <summary>Đổi ProductId âm thành Id vừa tạo. Còn Id không dương thì dòng trỏ mã không được khai.</summary>
    private static Result<IReadOnlyList<OrderLineInput>> BindProductIds(IReadOnlyList<OrderLineInput> lines,
        IReadOnlyList<CreatedProductDto> created)
    {
        var map = created.ToDictionary(c => c.TempId, c => c.ProductId);
        var bound = lines.Select(l => map.TryGetValue(l.ProductId, out var id) ? l with { ProductId = id } : l).ToList();
        return bound.Any(l => l.ProductId <= 0)
            ? Result<IReadOnlyList<OrderLineInput>>.Failure(ErrorCodes.CommonValidation, Messages.StagedProductUnbound, ResultKind.Validation)
            : Result<IReadOnlyList<OrderLineInput>>.Success(bound);
    }

    private List<ProductImage> ExtraImages(IReadOnlyList<string>? keys) =>
        (keys ?? []).Select((key, index) => new ProductImage
        {
            ImageKey = key.Trim(),
            SortOrder = (byte)(index + 1),
            CreatedAt = _clock.Now,
        }).ToList();

    public async Task<Result> CancelAsync(int orderId, CancelOrderDto dto, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var order = await _orders.GetByIdAsync(orderId);
        if (order is null) return Fail.NotFound();
        if (order.Status != OrderStatuses.New)
            return Result.Failure(ErrorCodes.OrderInvalidStatus, Messages.OrderInvalidStatus);
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return Result.Failure(ErrorCodes.CommonValidation, Messages.ReasonRequired, ResultKind.Validation);
        return await ChangeStatusAsync(order, dto.RowVer, OrderStatuses.Cancelled, dto.Reason, null, Messages.OrderCancelled, ct);
    }

    public async Task<Result> StopAsync(int orderId, StopOrderDto dto, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var order = await _orders.GetByIdAsync(orderId);
        if (order is null) return Fail.NotFound();
        if (!OrderStatuses.Stoppable.Contains(order.Status))
            return Result.Failure(ErrorCodes.OrderInvalidStatus, Messages.OrderInvalidStatus);
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return Result.Failure(ErrorCodes.CommonValidation, Messages.ReasonRequired, ResultKind.Validation);
        var ordered = await _lines.Query().Where(l => l.OrderId == orderId).SumAsync(l => (int?)l.OrderedQty, ct) ?? 0;
        if (ordered <= 0)
            return Result.Failure(ErrorCodes.OrderInvalidStatus, Messages.OrderStopNoLines, ResultKind.Validation);
        // Hàng đã xuất hết (đang giao + đã giao) thì không còn phần nào để dừng: chờ khách nhận là đơn sang Đã giao.
        if (await IsFullyDispatchedAsync(orderId, ct))
            return Result.Failure(ErrorCodes.OrderFullyDispatched, Messages.OrderFullyDispatched);
        return await ChangeStatusAsync(order, dto.RowVer, OrderStatuses.Stopped, dto.Reason, dto.Reason.Trim(), Messages.OrderStopped, ct);
    }

    public async Task<Result> StartProductionAsync(int orderId, StartOrderProductionDto dto, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var order = await _orders.GetByIdAsync(orderId);
        if (order is null) return Fail.NotFound();
        if (order.Status != OrderStatuses.New)
            return Result.Failure(ErrorCodes.OrderInvalidStatus, Messages.OrderInvalidStatus);
        if (order.MaterialStatus == OrderMaterialStatuses.Awaiting)
            return Result.Failure(ErrorCodes.OrderMaterialsAwaiting, Messages.OrderMaterialsAwaiting);
        return await ChangeStatusAsync(order, dto.RowVer, OrderStatuses.InProduction, null, null, Messages.OrderStarted, ct);
    }

    private async Task<Result> ChangeStatusAsync(CustomerOrder order, byte[] rowVer, string to, string? reason,
        string? stopReason, string successMessage, CancellationToken ct)
    {
        _orders.SetOriginalRowVersion(order, rowVer);
        if (RowVerMismatch(order.RowVer, rowVer) is { } stale) return stale;
        var from = order.Status;
        order.Status = to;
        if (stopReason is not null) order.StopReason = stopReason;
        order.UpdatedAt = _clock.Now;
        _audit.Add(new AuditEntry(nameof(CustomerOrder), order.OrderId, AuditActions.Update, new { Status = from },
            new { Status = to }, Clean(reason)));
        if (to is OrderStatuses.Cancelled or OrderStatuses.Stopped) await new OrderCloseCascade(_uow, _audit, _notifier).RunAsync(order, to, _clock.Now, ct);
        await _uow.SaveChangesAsync();
        return Result.Success(successMessage);
    }

    /// <summary>Kiểm đầu vào tạo/sửa đơn (unit test gọi service trực tiếp, không qua FluentValidation).</summary>
    private static Result? ValidateOrderInput(IOrderInput dto)
    {
        if (dto.Lines is null || dto.Lines.Count == 0)
            return Result.Failure(ErrorCodes.CommonValidation, Messages.OrderLinesRequired, ResultKind.Validation);
        if (dto.Lines.Any(l => l.OrderedQty is < 1 or > 1_000_000))
            return Result.Failure(ErrorCodes.CommonValidation, Messages.OrderQtyInvalid, ResultKind.Validation);
        if (dto.DueDate < dto.ReceivedDate)
            return Result.Failure(ErrorCodes.CommonValidation, Messages.OrderDueBeforeReceived, ResultKind.Validation);
        var staged = dto.StagedProducts ?? [];
        if (staged.Count > 0)
        {
            var lineIds = dto.Lines.Select(l => l.ProductId).ToHashSet();
            if (staged.Any(s => !lineIds.Contains(s.TempId)))
                return Result.Failure(ErrorCodes.CommonValidation, Messages.StagedProductUnused, ResultKind.Validation);
        }
        return null;
    }

    private static Result? RowVerMismatch(byte[] current, byte[] incoming) =>
        current is { Length: > 0 } && (incoming is null || !current.SequenceEqual(incoming))
            ? Result.Failure(ErrorCodes.CommonConcurrencyConflict, Messages.ConcurrencyConflict)
            : null;

    /// <summary>Số đặt không được nhỏ hơn số đã cắt (Σ ActualQty bàn cắt của dòng).</summary>
    private async Task<Result?> CheckOrderedQtyFloorAsync(IReadOnlyList<OrderLineInput> lines, CancellationToken ct)
    {
        var ids = lines.Where(l => l.OrderLineId is not null).Select(l => l.OrderLineId!.Value).Distinct().ToList();
        if (ids.Count == 0) return null;
        var cut = await _uow.Repository<CuttingLayLine>().Query().AsNoTracking()
            .Where(l => ids.Contains(l.OrderLineId))
            .GroupBy(l => l.OrderLineId)
            .Select(g => new { OrderLineId = g.Key, Qty = g.Sum(x => x.ActualQty) })
            .ToDictionaryAsync(x => x.OrderLineId, x => x.Qty, ct);
        foreach (var line in lines)
        {
            if (line.OrderLineId is not int id || !cut.TryGetValue(id, out var cutQty) || line.OrderedQty >= cutQty) continue;
            return Result.Failure(ErrorCodes.CommonValidation, string.Format(Messages.OrderQtyBelowCut, cutQty), ResultKind.Validation);
        }
        return null;
    }

    /// <summary>Số đã xuất kho (đang giao + đã giao) của các phiếu xuất đã phủ hết số đặt của đơn.</summary>
    private async Task<bool> IsFullyDispatchedAsync(int orderId, CancellationToken ct)
    {
        var ordered = await _lines.Query().Where(l => l.OrderId == orderId).SumAsync(l => l.OrderedQty, ct);
        var shipped = await _uow.Repository<DispatchNoteLine>().Query()
            .Where(l => l.DispatchNote.OrderId == orderId
                        && (l.DispatchNote.Status == DispatchStatuses.InDelivery || l.DispatchNote.Status == DispatchStatuses.Delivered))
            .SumAsync(l => l.IssueQty, ct);
        return ordered > 0 && shipped >= ordered;
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

    /// <summary>Khách phải tồn tại và đang hợp tác (đơn cũ giữ nguyên khách thì không kiểm); mã hàng phải đang dùng (chỉ kiểm mã hàng của dòng mới / dòng đổi mã hàng).</summary>
    private async Task<Result> CheckReferencesAsync(int customerId, IEnumerable<int> productIds, CancellationToken ct, int? currentCustomerId = null)
    {
        var customerStatus = await _orders.GetCustomerStatusAsync(customerId, ct);
        if (customerStatus is null)
            return Result.Failure(ErrorCodes.CommonNotFound, Messages.CustomerNotFound);
        if (customerStatus != RecordStatus.Active && customerId != currentCustomerId)
            return Result.Failure(ErrorCodes.CustomerNotActive, Messages.CustomerNotActive);
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
    /// Màu chưa có ở mã hàng thì tạo ProductColor trước (DEV-ALL-71).
    /// </summary>
    private async Task<Result<IReadOnlyDictionary<VariantKey, ProductVariant>>> ResolveVariantsAsync(
        IReadOnlyList<OrderLineInput> inputs, CancellationToken ct)
    {
        var productIds = inputs.Select(i => i.ProductId).Distinct().ToArray();
        var colors = (await _uow.Repository<ProductColor>().Query().Where(c => productIds.Contains(c.ProductId)).ToListAsync(ct))
            .ToDictionary(c => (c.ProductId, ColorKey(c.Color)));
        var existing = await _uow.Repository<ProductVariant>().Query().Include(v => v.ProductColor)
            .Where(v => productIds.Contains(v.ProductId)).ToListAsync(ct);
        var map = existing.ToDictionary(v => KeyOf(v.ProductId, v.ProductColor.Color, v.SizeCode));
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
                ProductId = input.ProductId,
                ProductColor = await ColorOfAsync(colors, input.ProductId, input.Color),
                SizeCode = input.SizeCode.Trim().ToUpperInvariant(),
                Status = RecordStatus.Active,
            };
            await _uow.Repository<ProductVariant>().AddAsync(created);
            map[key] = created;
        }
        return Result<IReadOnlyDictionary<VariantKey, ProductVariant>>.Success(map);
    }

    private static string ColorKey(string color) => color.Trim().ToUpperInvariant();

    /// <summary>Màu áo theo tên (không phân biệt hoa thường); chưa có thì tạo, dùng chung cho mọi size cùng màu trong lần lưu.</summary>
    private async Task<ProductColor> ColorOfAsync(Dictionary<(int, string), ProductColor> colors, int productId, string color)
    {
        var key = (productId, ColorKey(color));
        if (colors.TryGetValue(key, out var found)) return found;
        var created = new ProductColor { ProductId = productId, Color = color.Trim(), CreatedAt = _clock.Now };
        await _uow.Repository<ProductColor>().AddAsync(created);
        colors[key] = created;
        return created;
    }

    private static bool SameKey(OrderLine l, OrderLineInput i) =>
        l.ProductId == i.ProductId
        && string.Equals(l.ProductVariant.ProductColor.Color, i.Color.Trim(), StringComparison.OrdinalIgnoreCase)
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
        o.OrderCode, o.CustomerId, o.ReceivedDate, o.DueDate, o.DeliveryAddress, o.Note, o.Status, o.StopReason,
        Lines = o.OrderLines.OrderBy(l => l.ProductId).ThenBy(l => l.ProductVariant.ProductColor.Color).ThenBy(l => l.ProductVariant.SizeCode)
            .Select(l => new { l.OrderLineId, l.ProductId, l.ProductVariant.ProductColor.Color, l.ProductVariant.SizeCode, l.OrderedQty, l.CmtUnitPrice }).ToList(),
    };
}
