using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// KI-03 (UC52): tổ trưởng / tổ phó tổ may phân công công nhân × công đoạn của mã hàng được chia cho tổ (DEV-ALL-49). Đổi phân công = đóng dòng
/// cũ (<c>ValidTo</c>) rồi mở dòng mới, không sửa đè (giữ lịch sử). Tổ phải phân công đủ mọi công đoạn trước khi nhận BTP của đơn
/// (DEV-ALL-48, kiểm ở <see cref="MaterialIssueService"/>). Sau đó công đoạn ngoài phân công vẫn nhập được cho riêng ca đó (GBR-20).
/// </summary>
public sealed class OperationAssignmentService : IOperationAssignmentService
{
    private readonly IUnitOfWork _uow;
    private readonly ProductionGuard _guard;
    private readonly IClock _clock;

    public OperationAssignmentService(IUnitOfWork uow, ProductionGuard guard, IClock clock)
    {
        _uow = uow; _guard = guard; _clock = clock;
    }

    private IQueryable<OperationAssignment> Assignments => _uow.Repository<OperationAssignment>().Query();

    /// <summary>Mã hàng của đơn mà tổ được chia ít nhất một màu × size (DEV-ALL-49).</summary>
    private IQueryable<int> AllottedProductIds(int teamId, int orderId)
    {
        var allotted = _uow.Repository<OrderLineTeam>().Query().Where(a => a.SewingTeamId == teamId);
        return _uow.Repository<OrderLine>().Query()
            .Where(l => l.OrderId == orderId && allotted.Any(a => a.OrderLineId == l.OrderLineId))
            .Select(l => l.ProductId);
    }

    public async Task<Result<IReadOnlyList<OperationAssignmentDto>>> ListCurrentAsync(int orderId, CancellationToken ct)
    {
        if (orderId <= 0)
            return Result<IReadOnlyList<OperationAssignmentDto>>.Failure(ErrorCodes.AssignmentOrderInvalid, Messages.AssignmentOrderInvalid, ResultKind.Validation);
        var team = _guard.ResolveSewingTeam(null);
        if (!team.IsSuccess) return Fail.As<IReadOnlyList<OperationAssignmentDto>>(team);

        var items = await Assignments.AsNoTracking()
            .Where(a => a.SewingTeamId == team.Data && a.OrderId == orderId && a.ValidTo == null)
            .OrderBy(a => a.Operation.StationSequence).ThenBy(a => a.Worker.EmployeeCode)
            .Select(a => new OperationAssignmentDto(a.OperationAssignmentId, a.OrderId, a.OperationId,
                a.Operation.OperationCode, a.Operation.OperationName, a.Operation.ProductId,
                a.Operation.Product.ProductCode, a.WorkerId, a.Worker.EmployeeCode, a.Worker.FullName, a.ValidFrom))
            .ToListAsync(ct);
        return Result<IReadOnlyList<OperationAssignmentDto>>.Success(items);
    }

    public async Task<Result<IReadOnlyList<OperationOptionDto>>> ListOperationsAsync(int orderId, CancellationToken ct)
    {
        if (orderId <= 0)
            return Result<IReadOnlyList<OperationOptionDto>>.Failure(ErrorCodes.AssignmentOrderInvalid, Messages.AssignmentOrderInvalid, ResultKind.Validation);
        var team = _guard.ResolveSewingTeam(null);
        if (!team.IsSuccess) return Fail.As<IReadOnlyList<OperationOptionDto>>(team);

        var productIds = AllottedProductIds(team.Data, orderId);
        var items = await _uow.Repository<Operation>().Query().AsNoTracking()
            .Where(o => productIds.Contains(o.ProductId) && o.Status == OperationStatuses.Active
                && o.TeamType == TeamTypes.Sewing)
            .OrderBy(o => o.Product.ProductCode).ThenBy(o => o.StationSequence)
            .Select(o => new OperationOptionDto(o.OperationId, o.ProductId, o.Product.ProductCode, o.OperationCode,
                o.OperationName, o.StationSequence, o.IsFinalStep,
                o.OperationGroup == null ? null : o.OperationGroup.GroupName, o.SkillLevel, o.StandardTimeSec))
            .ToListAsync(ct);
        return Result<IReadOnlyList<OperationOptionDto>>.Success(items);
    }

    public async Task<Result<int>> AssignAsync(AssignOperationDto dto, CancellationToken ct)
    {
        var team = _guard.ResolveSewingTeam(null);
        if (!team.IsSuccess) return Fail.As<int>(team);

        var check = await ValidateAsync(team.Data, dto.OrderId, dto.OperationId, dto.WorkerId, ct);
        if (check is not null) return Fail.As<int>(check);

        // Đã phân công đúng công nhân này cho công đoạn này: không tạo dòng thứ hai (UX_OA_Current).
        var existing = await Assignments.AsNoTracking().FirstOrDefaultAsync(a => a.SewingTeamId == team.Data
            && a.OrderId == dto.OrderId && a.OperationId == dto.OperationId && a.WorkerId == dto.WorkerId
            && a.ValidTo == null, ct);
        if (existing is not null) return Result<int>.Success(existing.OperationAssignmentId, Messages.AssignmentSaved);

        var row = NewRow(team.Data, dto.OrderId, dto.OperationId, dto.WorkerId);
        await _uow.Repository<OperationAssignment>().AddAsync(row);
        await _uow.SaveChangesAsync();
        return Result<int>.Success(row.OperationAssignmentId, Messages.AssignmentSaved);
    }

    public async Task<Result<int>> AssignManyAsync(AssignOperationsDto dto, CancellationToken ct)
    {
        var team = _guard.ResolveSewingTeam(null);
        if (!team.IsSuccess) return Fail.As<int>(team);

        // Kiểm hợp lệ mọi công đoạn trước; một công đoạn sai thì không lưu công đoạn nào.
        var operationIds = dto.OperationIds.Distinct().ToList();
        var check = await ValidateManyAsync(team.Data, dto.OrderId, operationIds, dto.WorkerId, ct);
        if (check is not null) return Fail.As<int>(check);

        var already = await Assignments.AsNoTracking()
            .Where(a => a.SewingTeamId == team.Data && a.OrderId == dto.OrderId && a.WorkerId == dto.WorkerId && a.ValidTo == null
                        && operationIds.Contains(a.OperationId))
            .Select(a => a.OperationId).ToListAsync(ct);
        var rows = operationIds.Except(already)
            .Select(id => NewRow(team.Data, dto.OrderId, id, dto.WorkerId)).ToList();
        if (rows.Count > 0)
        {
            await _uow.Repository<OperationAssignment>().AddRangeAsync(rows);
            await _uow.SaveChangesAsync();
        }
        return Result<int>.Success(rows.Count, Messages.AssignmentSaved);
    }

    public async Task<Result<int>> ReplaceAsync(int assignmentId, ReplaceAssignmentDto dto, CancellationToken ct)
    {
        var team = _guard.ResolveSewingTeam(null);
        if (!team.IsSuccess) return Fail.As<int>(team);

        var old = await FindCurrentAsync(assignmentId, team.Data, ct);
        if (old is null)
            return Result<int>.Failure(ErrorCodes.CommonNotFound, Messages.AssignmentNotFound, ResultKind.NotFound);
        if (old.WorkerId == dto.NewWorkerId)
            return Result<int>.Success(old.OperationAssignmentId, Messages.AssignmentSaved);

        var check = await ValidateAsync(team.Data, old.OrderId, old.OperationId, dto.NewWorkerId, ct);
        if (check is not null) return Fail.As<int>(check);

        return await _uow.InTransactionAsync(async () =>
        {
            old.ValidTo = _clock.Today;   // đóng dòng cũ, giữ lại để xem lịch sử
            await _uow.SaveChangesAsync();   // đóng trước khi mở dòng mới để không vướng UX_OA_Current
            var row = NewRow(team.Data, old.OrderId, old.OperationId, dto.NewWorkerId);
            await _uow.Repository<OperationAssignment>().AddAsync(row);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(row.OperationAssignmentId, Messages.AssignmentSaved);
        });
    }

    public async Task<Result> CloseAsync(int assignmentId, CancellationToken ct)
    {
        var team = _guard.ResolveSewingTeam(null);
        if (!team.IsSuccess) return team;

        var row = await FindCurrentAsync(assignmentId, team.Data, ct);
        if (row is null)
            return Result.Failure(ErrorCodes.CommonNotFound, Messages.AssignmentNotFound, ResultKind.NotFound);
        if (await FindOpenOrderAsync(row.OrderId, ct) is null)
            return Result.Failure(ErrorCodes.AssignmentOrderInvalid, Messages.AssignmentOrderInvalid);

        row.ValidTo = _clock.Today;
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.AssignmentClosed);
    }

    /// <summary>Chép các phân công đang hiệu lực của tổ từ đơn cũ sang đơn mới cho những công đoạn đang dùng mà mã
    /// hàng cũng có trong đơn mới; bỏ qua dòng đã có và công nhân đã nghỉ / chuyển tổ.</summary>
    public async Task<Result<int>> CopyAsync(CopyAssignmentsDto dto, CancellationToken ct)
    {
        var team = _guard.ResolveSewingTeam(null);
        if (!team.IsSuccess) return Fail.As<int>(team);
        if (dto.FromOrderId == dto.ToOrderId)
            return Result<int>.Failure(ErrorCodes.AssignmentOrderInvalid, Messages.AssignmentCopySameOrder, ResultKind.Validation);

        var target = await FindOpenOrderAsync(dto.ToOrderId, ct);
        if (target is null)
            return Result<int>.Failure(ErrorCodes.AssignmentOrderInvalid, Messages.AssignmentOrderInvalid);

        var targetProductIds = await AllottedProductIds(team.Data, dto.ToOrderId).ToListAsync(ct);

        var sources = await Assignments.AsNoTracking()
            .Where(a => a.SewingTeamId == team.Data && a.OrderId == dto.FromOrderId && a.ValidTo == null
                        && a.Operation.Status == OperationStatuses.Active && a.Operation.TeamType == TeamTypes.Sewing
                        && targetProductIds.Contains(a.Operation.ProductId)
                        && a.Worker.Status == RecordStatus.Active && a.Worker.TeamId == team.Data
                        && a.Worker.Role.RoleCode == RoleCodes.Worker)
            .ToListAsync(ct);
        var current = await Assignments.AsNoTracking()
            .Where(a => a.SewingTeamId == team.Data && a.OrderId == dto.ToOrderId && a.ValidTo == null)
            .Select(a => new { a.OperationId, a.WorkerId }).ToListAsync(ct);
        var taken = current.Select(c => (c.OperationId, c.WorkerId)).ToHashSet();

        var copies = sources.Where(s => !taken.Contains((s.OperationId, s.WorkerId)))
            .Select(s => NewRow(team.Data, dto.ToOrderId, s.OperationId, s.WorkerId)).ToList();
        if (copies.Count > 0)
        {
            await _uow.Repository<OperationAssignment>().AddRangeAsync(copies);
            await _uow.SaveChangesAsync();
        }
        return Result<int>.Success(copies.Count, string.Format(Messages.AssignmentCopied, copies.Count));
    }

    /// <summary>Đơn còn mở, công đoạn đang dùng và thuộc một mã hàng của đơn, công nhân đang làm của đúng tổ này.</summary>
    private Task<Result?> ValidateAsync(int teamId, int orderId, int operationId, int workerId, CancellationToken ct) =>
        ValidateManyAsync(teamId, orderId, [operationId], workerId, ct);

    /// <summary>
    /// Như ValidateAsync cho nhiều công đoạn của cùng một công nhân, đọc đơn / công đoạn / mã hàng / công nhân mỗi thứ một lần
    /// thay vì một lượt truy vấn cho từng công đoạn. Công đoạn sai đầu tiên (theo thứ tự gửi lên) quyết định lỗi trả về.
    /// </summary>
    private async Task<Result?> ValidateManyAsync(int teamId, int orderId, IReadOnlyList<int> operationIds, int workerId,
        CancellationToken ct)
    {
        if (await FindOpenOrderAsync(orderId, ct) is null)
            return Result.Failure(ErrorCodes.AssignmentOrderInvalid, Messages.AssignmentOrderInvalid);

        var operations = await _uow.Repository<Operation>().Query().AsNoTracking()
            .Where(o => operationIds.Contains(o.OperationId))
            .Select(o => new { o.OperationId, o.ProductId, o.Status, o.TeamType }).ToDictionaryAsync(o => o.OperationId, ct);
        var productsInOrder = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => l.OrderId == orderId).Select(l => l.ProductId).Distinct().ToListAsync(ct);
        var allotted = await AllottedProductIds(teamId, orderId).Distinct().ToListAsync(ct);
        foreach (var operationId in operationIds)
        {
            if (!operations.TryGetValue(operationId, out var operation)) return Fail.NotFound();
            if (operation.Status != OperationStatuses.Active)
                return Result.Failure(ErrorCodes.OperationNotActive, Messages.OperationNotActive);
            if (operation.TeamType != TeamTypes.Sewing)
                return Result.Failure(ErrorCodes.AssignmentOperationNotSewing, Messages.AssignmentOperationNotSewing);
            if (!productsInOrder.Contains(operation.ProductId))
                return Result.Failure(ErrorCodes.OperationProductMismatch, Messages.OperationProductMismatch);
            if (!allotted.Contains(operation.ProductId))
                return Result.Failure(ErrorCodes.AssignmentProductNotAllotted, Messages.AssignmentProductNotAllotted);
        }

        var workerOk = await _uow.Repository<Employee>().Query().AsNoTracking()
            .AnyAsync(e => e.EmployeeId == workerId && e.TeamId == teamId && e.Status == RecordStatus.Active
                           && e.Role.RoleCode == RoleCodes.Worker, ct);
        return workerOk ? null : Result.Failure(ErrorCodes.AssignmentWorkerInvalid, Messages.AssignmentWorkerInvalid);
    }

    private Task<CustomerOrder?> FindOpenOrderAsync(int orderId, CancellationToken ct) => FindOpenOrderCoreAsync(orderId, OrderStatuses.Closed.ToArray(), ct);

    private Task<CustomerOrder?> FindOpenOrderCoreAsync(int orderId, string[] closed, CancellationToken ct) =>
        _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == orderId && !closed.Contains(o.Status), ct);

    private Task<OperationAssignment?> FindCurrentAsync(int assignmentId, int teamId, CancellationToken ct) =>
        Assignments.FirstOrDefaultAsync(a => a.OperationAssignmentId == assignmentId && a.SewingTeamId == teamId
                                             && a.ValidTo == null, ct);

    private OperationAssignment NewRow(int teamId, int orderId, int operationId, int workerId) => new()
    {
        SewingTeamId = teamId, OrderId = orderId, OperationId = operationId, WorkerId = workerId,
        ValidFrom = _clock.Today, ValidTo = null, AssignedAt = _clock.Now,
    };
}
