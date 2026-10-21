using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Production;

/// <summary>KI-01 (UC25): công đoạn của mã hàng. Không xóa, chỉ ngừng dùng (sản lượng cũ vẫn trỏ tới).</summary>
public sealed class OperationService : IOperationService
{
    private readonly IUnitOfWork _uow;
    private readonly IPieceRateService _rates;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;

    public OperationService(IUnitOfWork uow, IPieceRateService rates, IAuditLogService audit, IClock clock)
    {
        _uow = uow; _rates = rates; _audit = audit; _clock = clock;
    }

    private IQueryable<Operation> Operations => _uow.Repository<Operation>().Query();

    public async Task<Result<IReadOnlyList<OperationDto>>> ListAsync(int productId, bool includeDiscontinued, CancellationToken ct)
    {
        var items = await Operations.AsNoTracking()
            .Where(o => o.ProductId == productId && (includeDiscontinued || o.Status == OperationStatuses.Active))
            .OrderBy(o => o.StationSequence).ThenBy(o => o.OperationCode)
            .Select(o => new OperationDto(o.OperationId, o.ProductId, o.Product.ProductCode, o.OperationCode,
                o.OperationName, o.StationSequence, o.SkillLevel, o.StandardTimeSec, o.TeamType, o.IsFinalStep,
                o.Status, null))
            .ToListAsync(ct);

        var today = _clock.Today;
        var result = new List<OperationDto>(items.Count);
        foreach (var item in items)
        {
            var rate = await _rates.FindEffectiveAsync(item.OperationId, today, ct);
            result.Add(item with
            {
                TeamType = CodeText.ToPascal(item.TeamType), Status = CodeText.ToPascal(item.Status),
                CurrentRate = rate?.Rate,
            });
        }
        return Result<IReadOnlyList<OperationDto>>.Success(result);
    }

    public async Task<Result<int>> CreateAsync(SaveOperationDto dto, CancellationToken ct)
    {
        var product = await _uow.Repository<Product>().Query().AsNoTracking()
            .AnyAsync(p => p.ProductId == dto.ProductId, ct);
        if (!product) return Fail.NotFound<int>();

        var code = dto.OperationCode.Trim().ToUpperInvariant();
        var check = await CheckAsync(dto, code, null, ct);
        if (check is not null) return Fail.As<int>(check);

        var operation = new Operation { ProductId = dto.ProductId, Status = OperationStatuses.Active };
        Apply(operation, dto, code);

        return await _uow.InTransactionAsync(async () =>   // log cần OperationId nên lưu trước
        {
            await _uow.Repository<Operation>().AddAsync(operation);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(Operation), operation.OperationId, AuditActions.Insert, null, Snapshot(operation)));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(operation.OperationId, Messages.Saved);
        });
    }

    public async Task<Result> UpdateAsync(int operationId, SaveOperationDto dto, CancellationToken ct)
    {
        var operation = await _uow.Repository<Operation>().GetByIdAsync(operationId);
        if (operation is null) return Fail.NotFound();

        var code = dto.OperationCode.Trim().ToUpperInvariant();
        // Mã hàng của công đoạn không đổi: sản lượng và đơn giá cũ gắn với mã hàng đó.
        var check = await CheckAsync(dto with { ProductId = operation.ProductId }, code, operation, ct);
        if (check is not null) return check;

        var before = Snapshot(operation);
        Apply(operation, dto, code);
        _audit.Add(new AuditEntry(nameof(Operation), operationId, AuditActions.Update, before, Snapshot(operation)));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    public async Task<Result> DiscontinueAsync(int operationId, CancellationToken ct)
    {
        var operation = await _uow.Repository<Operation>().GetByIdAsync(operationId);
        if (operation is null) return Fail.NotFound();
        if (operation.Status == OperationStatuses.Discontinued) return Result.Success(Messages.Saved);

        operation.Status = OperationStatuses.Discontinued;
        _audit.Add(new AuditEntry(nameof(Operation), operationId, AuditActions.Update,
            new { Status = OperationStatuses.Active }, new { operation.Status }));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    public async Task<Result> ReactivateAsync(int operationId, CancellationToken ct)
    {
        var operation = await _uow.Repository<Operation>().GetByIdAsync(operationId);
        if (operation is null) return Fail.NotFound();
        if (operation.Status == OperationStatuses.Active) return Result.Success(Messages.Saved);

        // Dùng lại công đoạn cuối đã ngừng thì không được có công đoạn cuối khác đang dùng.
        if (operation.IsFinalStep && await FinalStepExistsAsync(operation.ProductId, operationId, ct))
            return Result.Failure(ErrorCodes.OperationFinalStepExists, Messages.OperationFinalStepExists);

        operation.Status = OperationStatuses.Active;
        _audit.Add(new AuditEntry(nameof(Operation), operationId, AuditActions.Update,
            new { Status = OperationStatuses.Discontinued }, new { operation.Status }));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    /// <summary>Mã trùng trong mã hàng và "đúng một công đoạn may cuối đang dùng" — báo rõ trước khi DB chặn.</summary>
    private async Task<Result?> CheckAsync(SaveOperationDto dto, string code, Operation? existing, CancellationToken ct)
    {
        var exceptId = existing?.OperationId;
        var duplicate = await Operations.AsNoTracking().AnyAsync(
            o => o.ProductId == dto.ProductId && o.OperationCode == code && (exceptId == null || o.OperationId != exceptId), ct);
        if (duplicate) return Result.Failure(ErrorCodes.OperationCodeDuplicate, Messages.OperationCodeDuplicate);

        var willBeActive = existing is null || existing.Status == OperationStatuses.Active;
        if (dto.IsFinalStep && willBeActive && await FinalStepExistsAsync(dto.ProductId, exceptId, ct))
            return Result.Failure(ErrorCodes.OperationFinalStepExists, Messages.OperationFinalStepExists);
        return null;
    }

    private Task<bool> FinalStepExistsAsync(int productId, int? exceptId, CancellationToken ct) =>
        Operations.AsNoTracking().AnyAsync(o => o.ProductId == productId && o.IsFinalStep
            && o.Status == OperationStatuses.Active && (exceptId == null || o.OperationId != exceptId), ct);

    private static void Apply(Operation operation, SaveOperationDto dto, string code)
    {
        operation.OperationCode = code;
        operation.OperationName = dto.OperationName.Trim();
        operation.StationSequence = dto.StationSequence;
        operation.SkillLevel = dto.SkillLevel;
        operation.StandardTimeSec = dto.StandardTimeSec;
        operation.TeamType = string.IsNullOrWhiteSpace(dto.TeamType) ? TeamTypes.Sewing : CodeText.ToCode(dto.TeamType);
        operation.IsFinalStep = dto.IsFinalStep;
    }

    private static object Snapshot(Operation o) => new
    {
        o.ProductId, o.OperationCode, o.OperationName, o.StationSequence, o.SkillLevel, o.StandardTimeSec,
        o.TeamType, o.IsFinalStep, o.Status,
    };
}
