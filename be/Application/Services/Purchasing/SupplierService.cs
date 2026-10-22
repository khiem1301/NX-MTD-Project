using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Purchasing;

/// <summary>UC14 (MI-07): Admin quản lý nhà cung cấp. Không xóa, chỉ ngừng hợp tác (đơn mua cũ vẫn trỏ tới).</summary>
public sealed class SupplierService : ISupplierService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;

    public SupplierService(IUnitOfWork uow, IAuditLogService audit, IClock clock)
    {
        _uow = uow; _audit = audit; _clock = clock;
    }

    private IQueryable<Supplier> Suppliers => _uow.Repository<Supplier>().Query();

    public async Task<Result<PagedResult<SupplierDto>>> ListAsync(SupplierQuery query, CancellationToken ct)
    {
        var status = string.IsNullOrWhiteSpace(query.Status) ? null : CodeText.ToCode(query.Status);
        var keyword = query.Keyword?.Trim();

        var page = await Suppliers.AsNoTracking()
            .Where(s => status == null || s.Status == status)
            .Where(s => string.IsNullOrEmpty(keyword) || s.SupplierName.Contains(keyword)
                || (s.Phone != null && s.Phone.Contains(keyword)))
            .OrderBy(s => s.SupplierName)
            .Select(s => new SupplierDto(s.SupplierId, s.SupplierName, s.Phone, s.Email, s.Address, s.MaterialTypes,
                s.Note, s.Status, s.CreatedAt, s.PurchaseOrders.Count))
            .ToPagedAsync(query, ct);

        return Result<PagedResult<SupplierDto>>.Success(
            page with { Items = page.Items.Select(WithPascalStatus).ToList() });
    }

    public async Task<Result<SupplierDetailDto>> GetAsync(int supplierId, CancellationToken ct)
    {
        var dto = await Suppliers.AsNoTracking().Where(s => s.SupplierId == supplierId)
            .Select(s => new SupplierDto(s.SupplierId, s.SupplierName, s.Phone, s.Email, s.Address, s.MaterialTypes,
                s.Note, s.Status, s.CreatedAt, s.PurchaseOrders.Count))
            .FirstOrDefaultAsync(ct);
        if (dto is null) return Fail.NotFound<SupplierDetailDto>();

        var history = await _audit.GetHistoryAsync(nameof(Supplier), supplierId, ct);
        return Result<SupplierDetailDto>.Success(new SupplierDetailDto(WithPascalStatus(dto), history));
    }

    public async Task<Result<int>> CreateAsync(SaveSupplierDto dto, CancellationToken ct)
    {
        var name = dto.SupplierName.Trim();
        if (await NameTakenAsync(name, null, ct))
            return Result<int>.Failure(ErrorCodes.SupplierNameDuplicate, Messages.SupplierNameDuplicate);

        var supplier = new Supplier { Status = RecordStatus.Active, CreatedAt = _clock.Now };
        Apply(supplier, dto, name);

        return await _uow.InTransactionAsync(async () =>   // log cần SupplierId nên lưu trước
        {
            await _uow.Repository<Supplier>().AddAsync(supplier);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(Supplier), supplier.SupplierId, AuditActions.Insert, null, Snapshot(supplier)));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(supplier.SupplierId, Messages.SupplierSaved);
        });
    }

    public async Task<Result> UpdateAsync(int supplierId, SaveSupplierDto dto, CancellationToken ct)
    {
        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(supplierId);
        if (supplier is null) return Fail.NotFound();

        var name = dto.SupplierName.Trim();
        if (await NameTakenAsync(name, supplierId, ct))
            return Result.Failure(ErrorCodes.SupplierNameDuplicate, Messages.SupplierNameDuplicate);

        var before = Snapshot(supplier);
        Apply(supplier, dto, name);
        _audit.Add(new AuditEntry(nameof(Supplier), supplierId, AuditActions.Update, before, Snapshot(supplier)));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.SupplierSaved);
    }

    public async Task<Result> DiscontinueAsync(int supplierId, CancellationToken ct)
    {
        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(supplierId);
        if (supplier is null) return Fail.NotFound();
        if (supplier.Status == RecordStatus.Discontinued) return Result.Success(Messages.SupplierDiscontinued);

        supplier.Status = RecordStatus.Discontinued;
        _audit.Add(new AuditEntry(nameof(Supplier), supplierId, AuditActions.Update,
            new { Status = RecordStatus.Active }, new { supplier.Status }));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.SupplierDiscontinued);
    }

    /// <summary>Gọi lại khi đã ACTIVE không lỗi, không đổi gì (giống tổ, TeamService).</summary>
    public async Task<Result> ReactivateAsync(int supplierId, CancellationToken ct)
    {
        var supplier = await _uow.Repository<Supplier>().GetByIdAsync(supplierId);
        if (supplier is null) return Fail.NotFound();
        if (supplier.Status == RecordStatus.Active) return Result.Success(Messages.Saved);

        supplier.Status = RecordStatus.Active;
        _audit.Add(new AuditEntry(nameof(Supplier), supplierId, AuditActions.Update,
            new { Status = RecordStatus.Discontinued }, new { supplier.Status }));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    public async Task<Result<IReadOnlyList<SupplierOptionDto>>> GetActiveOptionsAsync(CancellationToken ct)
    {
        var options = await Suppliers.AsNoTracking()
            .Where(s => s.Status == RecordStatus.Active)
            .OrderBy(s => s.SupplierName)
            .Select(s => new SupplierOptionDto(s.SupplierId, s.SupplierName))
            .ToListAsync(ct);
        return Result<IReadOnlyList<SupplierOptionDto>>.Success(options);
    }

    private Task<bool> NameTakenAsync(string name, int? exceptId, CancellationToken ct) =>
        Suppliers.AsNoTracking().AnyAsync(s => s.SupplierName == name && (exceptId == null || s.SupplierId != exceptId), ct);

    private static void Apply(Supplier supplier, SaveSupplierDto dto, string name)
    {
        supplier.SupplierName = name;
        supplier.Phone = Clean(dto.Phone);
        supplier.Email = Clean(dto.Email);
        supplier.Address = Clean(dto.Address);
        supplier.MaterialTypes = Clean(dto.MaterialTypes);
        supplier.Note = Clean(dto.Note);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object Snapshot(Supplier s) =>
        new { s.SupplierName, s.Phone, s.Email, s.Address, s.MaterialTypes, s.Note, s.Status };

    private static SupplierDto WithPascalStatus(SupplierDto dto) => dto with { Status = CodeText.ToPascal(dto.Status) };
}
