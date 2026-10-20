using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>UC37 / UC39: phiếu nhập NVL khách cấp — Thủ kho lập, Quản đốc xác nhận (tăng tồn) hoặc trả lại.</summary>
public sealed class MaterialReceiptService : IMaterialReceiptService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly IDocumentCodeGenerator _codes;
    private readonly IClock _clock;
    private readonly ILogger<MaterialReceiptService> _logger;

    public MaterialReceiptService(IUnitOfWork uow, IStockService stock, IDocumentCodeGenerator codes, IClock clock,
        ILogger<MaterialReceiptService> logger)
    {
        _uow = uow; _stock = stock; _codes = codes; _clock = clock; _logger = logger;
    }

    private IQueryable<MaterialReceipt> Receipts => _uow.Repository<MaterialReceipt>().Query();

    public async Task<Result<PagedResult<MaterialReceiptListItemDto>>> GetPagedAsync(MaterialReceiptQuery query,
        CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Receipts.AsNoTracking()
            .Where(r => string.IsNullOrEmpty(query.Status) || r.Status == query.Status)
            .Where(r => query.OrderId == null || r.OrderId == query.OrderId)
            .Where(r => string.IsNullOrEmpty(keyword) || r.ReceiptCode.Contains(keyword)
                || (r.Order != null && r.Order.OrderCode.Contains(keyword)))
            .OrderByDescending(r => r.MaterialReceiptId)
            .Select(r => new MaterialReceiptListItemDto(r.MaterialReceiptId, r.ReceiptCode, r.ReceiptType, r.OrderId,
                r.Order == null ? null : r.Order.OrderCode, r.Source, r.ReceiptDate, r.Status, r.SubmittedAt, r.ConfirmedAt,
                r.ConfirmedByNavigation == null ? null : r.ConfirmedByNavigation.FullName, r.MaterialReceiptLines.Count))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<MaterialReceiptListItemDto>>.Success(page);
    }

    public async Task<Result<MaterialReceiptDto>> GetByIdAsync(int receiptId, CancellationToken ct)
    {
        var dto = await Receipts.AsNoTracking().Where(r => r.MaterialReceiptId == receiptId)
            .Select(r => new MaterialReceiptDto(r.MaterialReceiptId, r.ReceiptCode, r.ReceiptType, r.OrderId,
                r.Order == null ? null : r.Order.OrderCode, r.Source, r.ReceiptDate, r.Status, r.ReturnReason, r.Note,
                r.SubmittedAt, r.ConfirmedAt, r.ConfirmedByNavigation == null ? null : r.ConfirmedByNavigation.FullName,
                r.MaterialReceiptLines.OrderBy(l => l.MaterialReceiptLineId).Select(l => new MaterialReceiptLineDto(
                    l.MaterialReceiptLineId, l.OrderMaterialId,
                    l.OrderMaterial != null ? l.OrderMaterial.MaterialId : l.MaterialId!.Value,
                    l.OrderMaterial != null ? l.OrderMaterial.Material.MaterialCode : l.Material!.MaterialCode,
                    l.OrderMaterial != null ? l.OrderMaterial.Material.MaterialName : l.Material!.MaterialName,
                    l.OrderMaterial != null ? l.OrderMaterial.Material.Unit : l.Material!.Unit,
                    l.OrderMaterial != null ? l.OrderMaterial.MaterialColor : l.MaterialColor ?? string.Empty,
                    l.AcceptedQty, l.ShortQty, l.DefectiveQty)).ToList()))
            .FirstOrDefaultAsync(ct);
        return dto is null ? WarehouseFailures.NotFound<MaterialReceiptDto>() : Result<MaterialReceiptDto>.Success(dto);
    }

    public async Task<Result<MaterialReceiptDto>> CreateAsync(SaveMaterialReceiptDto dto, CancellationToken ct)
    {
        var check = await ValidateReferencesAsync(dto, ct);
        if (check is not null) return check;

        var created = await _uow.InTransactionAsync(async () =>
        {
            var receipt = new MaterialReceipt
            {
                ReceiptCode = await _codes.NextAsync<MaterialReceipt>(DocumentPrefixes.MaterialReceipt, r => r.ReceiptCode, ct),
                ReceiptType = ReceiptTypes.New,
                Status = ReceiptStatuses.Pending,
                SubmittedAt = _clock.Now,
            };
            Apply(receipt, dto);
            await _uow.Repository<MaterialReceipt>().AddAsync(receipt);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(receipt.MaterialReceiptId);
        });
        return await GetByIdAsync(created.Data, ct);
    }

    public async Task<Result<MaterialReceiptDto>> UpdateAsync(int receiptId, SaveMaterialReceiptDto dto,
        CancellationToken ct)
    {
        var receipt = await Receipts.Include(r => r.MaterialReceiptLines)
            .FirstOrDefaultAsync(r => r.MaterialReceiptId == receiptId, ct);
        if (receipt is null) return WarehouseFailures.NotFound<MaterialReceiptDto>();
        if (receipt.Status is not (ReceiptStatuses.Pending or ReceiptStatuses.Returned))
            return WarehouseFailures.InvalidStatus<MaterialReceiptDto>();

        var check = await ValidateReferencesAsync(dto, ct);
        if (check is not null) return check;

        _uow.Repository<MaterialReceiptLine>().RemoveRange(receipt.MaterialReceiptLines.ToList());
        Apply(receipt, dto);
        receipt.Status = ReceiptStatuses.Pending;
        receipt.ReturnReason = null;
        receipt.SubmittedAt = _clock.Now;
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(receiptId, ct);
    }

    public Task<Result> ConfirmAsync(int receiptId, int confirmedByEmployeeId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var receipt = await Receipts.Include(r => r.MaterialReceiptLines).ThenInclude(l => l.OrderMaterial)
                .FirstOrDefaultAsync(r => r.MaterialReceiptId == receiptId, ct);
            if (receipt is null) return WarehouseFailures.NotFound();
            if (receipt.Status != ReceiptStatuses.Pending) return WarehouseFailures.InvalidStatus();

            var today = _clock.Today;
            var movements = receipt.MaterialReceiptLines.Where(l => l.AcceptedQty > 0)
                .Select(l => new StockMovement(
                    l.OrderMaterial is { } om
                        ? StockKey.Material(om.MaterialId, om.MaterialColor)
                        : StockKey.Material(l.MaterialId!.Value, l.MaterialColor ?? string.Empty),
                    l.AcceptedQty, StockDocTypes.MaterialReceipt, receipt.MaterialReceiptId, l.MaterialReceiptLineId, today))
                .ToList();
            if (movements.Count > 0)
            {
                var posted = await _stock.PostAsync(movements, ct);
                if (!posted.IsSuccess) return posted;
            }

            receipt.Status = ReceiptStatuses.Confirmed;
            receipt.ConfirmedBy = confirmedByEmployeeId;
            receipt.ConfirmedAt = _clock.Now;
            // TODO(thai, TH-13): dòng thiếu / lỗi → tự tạo đơn SHORT_DELIVERY cho Quản đốc (UC08) và cập nhật MaterialStatus.
            await _uow.SaveChangesAsync();
            _logger.LogInformation("Material receipt {ReceiptCode} confirmed by employee {EmployeeId}",
                receipt.ReceiptCode, confirmedByEmployeeId);
            return Result.Success(WarehouseMessages.ReceiptConfirmed);
        });

    public async Task<Result> ReturnAsync(int receiptId, ReasonDto dto, CancellationToken ct)
    {
        var receipt = await Receipts.FirstOrDefaultAsync(r => r.MaterialReceiptId == receiptId, ct);
        if (receipt is null) return WarehouseFailures.NotFound();
        if (receipt.Status != ReceiptStatuses.Pending) return WarehouseFailures.InvalidStatus();

        receipt.Status = ReceiptStatuses.Returned;
        receipt.ReturnReason = dto.Reason.Trim();
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.ReceiptReturned);
    }

    private async Task<Result<MaterialReceiptDto>?> ValidateReferencesAsync(SaveMaterialReceiptDto dto,
        CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId, ct);
        if (order is null) return WarehouseFailures.NotFound<MaterialReceiptDto>();
        if (OrderStatuses.Closed.Contains(order.Status))
            return Result<MaterialReceiptDto>.Failure(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);

        var ids = dto.Lines.Select(l => l.OrderMaterialId).ToList();
        var valid = await _uow.Repository<OrderMaterial>().Query()
            .CountAsync(om => ids.Contains(om.OrderMaterialId) && om.OrderId == dto.OrderId, ct);
        return valid == ids.Count
            ? null
            : Result<MaterialReceiptDto>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.OrderMaterialNotInOrder);
    }

    private static void Apply(MaterialReceipt receipt, SaveMaterialReceiptDto dto)
    {
        receipt.OrderId = dto.OrderId;
        receipt.Source = dto.Source;
        receipt.ReceiptDate = dto.ReceiptDate;
        receipt.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        // TODO(thai, TH-03): đính kèm ảnh chứng từ khi IAttachmentService (KH-07) sẵn sàng.
        receipt.MaterialReceiptLines = dto.Lines.Select(l => new MaterialReceiptLine
        {
            OrderMaterialId = l.OrderMaterialId,
            AcceptedQty = l.AcceptedQty,
            ShortQty = l.ShortQty,
            DefectiveQty = l.DefectiveQty,
        }).ToList();
    }
}
