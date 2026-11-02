using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-50: nhóm công đoạn của mã hàng, chỉ để nhóm và tìm kiếm; phân công, đơn giá, sản lượng vẫn theo từng công đoạn.
/// Vì vậy xóa nhóm chỉ đưa các công đoạn trong nhóm về chưa có nhóm, không ảnh hưởng dữ liệu sản xuất.
/// </summary>
public sealed class OperationGroupService : IOperationGroupService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogService _audit;

    public OperationGroupService(IUnitOfWork uow, IAuditLogService audit)
    {
        _uow = uow; _audit = audit;
    }

    private IQueryable<OperationGroup> Groups => _uow.Repository<OperationGroup>().Query();
    private IQueryable<Operation> Operations => _uow.Repository<Operation>().Query();

    public async Task<Result<IReadOnlyList<OperationGroupDto>>> ListAsync(int productId, CancellationToken ct)
    {
        if (productId <= 0)
            return Result<IReadOnlyList<OperationGroupDto>>.Failure(ErrorCodes.OperationInvalid, Messages.OperationProductRequired, ResultKind.Validation);

        var groups = await Groups.AsNoTracking().Where(g => g.ProductId == productId).OrderBy(g => g.GroupName)
            .Select(g => new { g.OperationGroupId, g.GroupName }).ToListAsync(ct);
        var groupIds = await Operations.AsNoTracking().Where(o => o.ProductId == productId && o.OperationGroupId != null)
            .Select(o => o.OperationGroupId!.Value).ToListAsync(ct);
        var items = groups.Select(g => new OperationGroupDto(g.OperationGroupId, g.GroupName, groupIds.Count(id => id == g.OperationGroupId))).ToList();
        return Result<IReadOnlyList<OperationGroupDto>>.Success(items);
    }

    public async Task<Result<int>> CreateAsync(CreateOperationGroupDto dto, CancellationToken ct)
    {
        var productExists = await _uow.Repository<Product>().Query().AsNoTracking().AnyAsync(p => p.ProductId == dto.ProductId, ct);
        if (!productExists) return Fail.NotFound<int>();

        var name = dto.GroupName.Trim();
        if (await NameTakenAsync(dto.ProductId, name, null, ct))
            return Result<int>.Failure(ErrorCodes.OperationGroupDuplicate, Messages.OperationGroupDuplicate);

        var group = new OperationGroup { ProductId = dto.ProductId, GroupName = name };
        return await _uow.InTransactionAsync(async () =>   // log cần OperationGroupId nên lưu trước
        {
            await _uow.Repository<OperationGroup>().AddAsync(group);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(OperationGroup), group.OperationGroupId, AuditActions.Insert, null, new { group.ProductId, group.GroupName }));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(group.OperationGroupId, Messages.Saved);
        });
    }

    public async Task<Result> RenameAsync(int groupId, RenameOperationGroupDto dto, CancellationToken ct)
    {
        var group = await Groups.FirstOrDefaultAsync(g => g.OperationGroupId == groupId, ct);
        if (group is null) return Fail.NotFound();

        var name = dto.GroupName.Trim();
        if (group.GroupName == name) return Result.Success(Messages.Saved);
        if (await NameTakenAsync(group.ProductId, name, groupId, ct))
            return Result.Failure(ErrorCodes.OperationGroupDuplicate, Messages.OperationGroupDuplicate);

        var before = group.GroupName;
        group.GroupName = name;
        _audit.Add(new AuditEntry(nameof(OperationGroup), groupId, AuditActions.Update, new { GroupName = before }, new { GroupName = name }));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    public async Task<Result> DeleteAsync(int groupId, CancellationToken ct)
    {
        var group = await Groups.FirstOrDefaultAsync(g => g.OperationGroupId == groupId, ct);
        if (group is null) return Fail.NotFound();

        var members = await Operations.Where(o => o.OperationGroupId == groupId).ToListAsync(ct);
        return await _uow.InTransactionAsync(async () =>
        {
            foreach (var operation in members)
            {
                operation.OperationGroup = null;
                operation.OperationGroupId = null;
            }
            await _uow.SaveChangesAsync();   // bỏ nhóm khỏi công đoạn trước khi xóa nhóm để không vướng FK_Operation_Group
            _uow.Repository<OperationGroup>().RemoveRange([group]);
            _audit.Add(new AuditEntry(nameof(OperationGroup), groupId, AuditActions.Delete,
                new { group.ProductId, group.GroupName, OperationIds = members.Select(o => o.OperationId).ToList() }, null));
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.Saved);
        });
    }

    /// <summary>Xếp nhiều công đoạn vào một nhóm, hoặc bỏ nhóm khi <c>OperationGroupId</c> null. Trả về số công đoạn thực sự đổi nhóm.</summary>
    public async Task<Result<int>> SetOperationsGroupAsync(SetOperationsGroupDto dto, CancellationToken ct)
    {
        if (dto.OperationGroupId is { } groupId
            && !await Groups.AsNoTracking().AnyAsync(g => g.OperationGroupId == groupId && g.ProductId == dto.ProductId, ct))
            return Fail.NotFound<int>();

        var operations = await Operations.Where(o => dto.OperationIds.Contains(o.OperationId)).ToListAsync(ct);
        if (operations.Count != dto.OperationIds.Count) return Fail.NotFound<int>();
        if (operations.Any(o => o.ProductId != dto.ProductId))
            return Result<int>.Failure(ErrorCodes.OperationInvalid, Messages.OperationGroupProductMismatch);

        var changed = operations.Where(o => o.OperationGroupId != dto.OperationGroupId).ToList();
        if (changed.Count == 0) return Result<int>.Success(0);
        foreach (var operation in changed)
        {
            _audit.Add(new AuditEntry(nameof(Operation), operation.OperationId, AuditActions.Update,
                new { operation.OperationGroupId }, new { dto.OperationGroupId }));
            operation.OperationGroup = null;
            operation.OperationGroupId = dto.OperationGroupId;
        }
        await _uow.SaveChangesAsync();
        return Result<int>.Success(changed.Count, Messages.Saved);
    }

    private Task<bool> NameTakenAsync(int productId, string name, int? exceptId, CancellationToken ct) =>
        Groups.AsNoTracking().AnyAsync(g => g.ProductId == productId && g.GroupName == name
            && (exceptId == null || g.OperationGroupId != exceptId), ct);
}
