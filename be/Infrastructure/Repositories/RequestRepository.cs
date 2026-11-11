using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Repositories;

public sealed class RequestRepository : GenericRepository<Request>, IRequestRepository
{
    public RequestRepository(NxMtdDbContext db) : base(db) { }

    public Task<Request?> GetWithLinesAsync(int requestId, CancellationToken ct) =>
        _context.Requests.Include(r => r.RequestLines).FirstOrDefaultAsync(r => r.RequestId == requestId, ct);

    public Task<RequestHeader?> GetHeaderAsync(int requestId, CancellationToken ct) =>
        _context.Requests.AsNoTracking().Where(r => r.RequestId == requestId)
            .Select(r => new RequestHeader(r.RequestId, r.RequestTypeCode, r.Status, r.CreatedBy, r.CreatedByTeamId,
                r.HandlerRoleCode, r.HandlerTeamId))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<int>> TeamLeaderIdsAsync(int teamId, CancellationToken ct) =>
        await _context.Employees.AsNoTracking()
            .Where(e => e.TeamId == teamId && e.Status == RecordStatus.Active
                        && (e.Role.RoleCode == RoleCodes.TeamLeader || e.Role.RoleCode == RoleCodes.TeamDeputy))
            .Select(e => e.EmployeeId).ToListAsync(ct);

    public async Task<string?> GetMaxCodeAsync(string prefix, CancellationToken ct)
    {
        var saved = await _context.Requests.AsNoTracking().Where(r => r.RequestCode.StartsWith(prefix))
            .OrderByDescending(r => r.RequestCode).Select(r => r.RequestCode).FirstOrDefaultAsync(ct);
        // Đơn vừa thêm trong cùng lần lưu (một lần duyệt tạo hai đơn hệ thống) chưa có trong DB nhưng đã chiếm mã.
        var pending = _context.ChangeTracker.Entries<Request>().Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity.RequestCode).Where(code => code != null && code.StartsWith(prefix, StringComparison.Ordinal));
        return pending.Append(saved).Where(code => code != null).OrderByDescending(code => code, StringComparer.Ordinal).FirstOrDefault();
    }

    public async Task<PagedResult<RequestListItemDto>> ListAsync(RequestListFilter f, CancellationToken ct)
    {
        var q = _context.Requests.AsNoTracking();
        if (f.CreatedBy is int by) q = q.Where(r => r.CreatedBy == by);
        if (f.CreatedByTeamId is int team) q = q.Where(r => r.CreatedByTeamId == team);
        if (f.RequestTypes is { } types)
        {
            var roles = f.OtherHandlerRoleCodes ?? Array.Empty<string>();
            var handlerTeam = f.OtherHandlerTeamId;
            q = q.Where(r => types.Contains(r.RequestTypeCode)
                             || (r.RequestTypeCode == RequestTypes.Other
                                 && (roles.Contains(r.HandlerRoleCode!)
                                     || (handlerTeam != null && r.HandlerRoleCode == RoleCodes.TeamLeader && r.HandlerTeamId == handlerTeam))));
        }
        if (f.Statuses is { } statuses) q = q.Where(r => statuses.Contains(r.Status));
        if (!string.IsNullOrWhiteSpace(f.Page.Keyword))
        {
            var k = f.Page.Keyword.Trim();
            q = q.Where(r => r.RequestCode.Contains(k) || (r.Title != null && r.Title.Contains(k)));
        }

        var page = await q.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.RequestId)
            .Select(r => new
            {
                r.RequestId,
                r.RequestCode,
                r.RequestTypeCode,
                r.Status,
                r.Title,
                r.TargetDate,
                CreatedByName = r.CreatedByNavigation == null ? null : r.CreatedByNavigation.FullName,
                TeamName = r.CreatedByTeam == null ? null : r.CreatedByTeam.TeamName,
                r.CreatedAt,
                r.DecidedAt,
            })
            .ToPagedAsync(f.Page, ct);

        var items = page.Items.Select(r => new RequestListItemDto(r.RequestId, r.RequestCode,
            CodeText.ToPascal(r.RequestTypeCode), CodeText.ToPascal(r.Status), r.Title, r.TargetDate,
            r.CreatedByName, r.TeamName, r.CreatedAt, r.DecidedAt)).ToList();
        return new PagedResult<RequestListItemDto>(items, page.Page, page.PageSize, page.TotalItems);
    }

    public async Task<RequestDetailDto?> GetDetailAsync(int requestId, CancellationToken ct)
    {
        var dto = await _context.Requests.AsNoTracking().Where(r => r.RequestId == requestId)
            .Select(r => new RequestDetailDto
            {
                RequestId = r.RequestId,
                RequestCode = r.RequestCode,
                RequestType = r.RequestTypeCode,
                Status = r.Status,
                Title = r.Title,
                Content = r.Content,
                TargetDate = r.TargetDate,
                OrderId = r.OrderId,
                OrderCode = r.Order == null ? null : r.Order.OrderCode,
                WorkOrderId = r.WorkOrderId, WorkOrderCode = r.WorkOrder == null ? null : r.WorkOrder.WorkOrderCode,
                CreatedBy = r.CreatedBy,
                CreatedByName = r.CreatedByNavigation == null ? null : r.CreatedByNavigation.FullName,
                CreatedByTeamId = r.CreatedByTeamId,
                TeamName = r.CreatedByTeam == null ? null : r.CreatedByTeam.TeamName,
                HandlerRole = r.HandlerRoleCode,
                HandlerTeamId = r.HandlerTeamId,
                HandlerTeamName = r.HandlerTeam == null ? null : r.HandlerTeam.TeamName,
                CreatedAt = r.CreatedAt,
                DecidedAt = r.DecidedAt,
                DecisionNote = r.DecisionNote,
                ResultNote = r.ResultNote,
                CompletedAt = r.CompletedAt,
                ShortageResolution = r.ShortageResolution,
                RowVer = r.RowVer,
                Lines = r.RequestLines.OrderBy(l => l.RequestLineId).Select(l => new RequestLineDto(
                    l.RequestLineId, l.MaterialId, l.Material == null ? null : l.Material.MaterialName, l.Material == null ? null : l.Material.Color,
                    l.OrderLineId,
                    l.OrderLine == null ? null : l.OrderLine.ProductVariant.Product.ProductCode + " · " + l.OrderLine.ProductVariant.ProductColor.Color + " · " + l.OrderLine.ProductVariant.SizeCode,
                    l.ProductComponentMaterialId, l.ProductComponentMaterial == null ? null : l.ProductComponentMaterial.ProductComponent.ComponentName + " – " + l.ProductComponentMaterial.Material.MaterialName + (l.ProductComponentMaterial.Material.Color == "" ? "" : " " + l.ProductComponentMaterial.Material.Color),
                    l.ItemDescription, l.Quantity, l.ReplenishQty)).ToList(),
            })
            .FirstOrDefaultAsync(ct);
        if (dto is null) return null;
        // Mã DB → PascalCase cho API (D22); service đổi ngược khi cần kiểm quyền.
        return dto with
        {
            RequestType = CodeText.ToPascal(dto.RequestType),
            Status = CodeText.ToPascal(dto.Status),
            ShortageResolution = dto.ShortageResolution is null ? null : CodeText.ToPascal(dto.ShortageResolution),
            HandlerRole = dto.HandlerRole is null ? null : Roles.FromRoleCode(dto.HandlerRole),
        };
    }
}
