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
            .Select(r => new RequestHeader(r.RequestId, r.RequestTypeCode, r.Status, r.CreatedBy, r.CreatedByTeamId))
            .FirstOrDefaultAsync(ct);

    public Task<string?> GetMaxCodeAsync(string prefix, CancellationToken ct) =>
        _context.Requests.AsNoTracking().Where(r => r.RequestCode.StartsWith(prefix))
            .OrderByDescending(r => r.RequestCode).Select(r => r.RequestCode).FirstOrDefaultAsync(ct);

    public async Task<PagedResult<RequestListItemDto>> ListAsync(RequestListFilter f, CancellationToken ct)
    {
        var q = _context.Requests.AsNoTracking();
        if (f.CreatedBy is int by) q = q.Where(r => r.CreatedBy == by);
        if (f.CreatedByTeamId is int team) q = q.Where(r => r.CreatedByTeamId == team);
        if (f.RequestTypes is { } types) q = q.Where(r => types.Contains(r.RequestTypeCode));
        if (f.Statuses is { } statuses) q = q.Where(r => statuses.Contains(r.Status));
        if (!string.IsNullOrWhiteSpace(f.Page.Keyword))
        {
            var k = f.Page.Keyword.Trim();
            q = q.Where(r => r.RequestCode.Contains(k) || (r.Title != null && r.Title.Contains(k)));
        }

        var page = await q.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.RequestId)
            .Select(r => new
            {
                r.RequestId, r.RequestCode, r.RequestTypeCode, r.Status, r.Title, r.TargetDate,
                CreatedByName = r.CreatedByNavigation == null ? null : r.CreatedByNavigation.FullName,
                TeamName = r.CreatedByTeam == null ? null : r.CreatedByTeam.TeamName,
                r.CreatedAt, r.DecidedAt,
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
                RequestId = r.RequestId, RequestCode = r.RequestCode, RequestType = r.RequestTypeCode, Status = r.Status,
                Title = r.Title, Content = r.Content, TargetDate = r.TargetDate, OrderId = r.OrderId,
                OrderCode = r.Order == null ? null : r.Order.OrderCode,
                CreatedBy = r.CreatedBy, CreatedByName = r.CreatedByNavigation == null ? null : r.CreatedByNavigation.FullName,
                CreatedByTeamId = r.CreatedByTeamId, TeamName = r.CreatedByTeam == null ? null : r.CreatedByTeam.TeamName,
                CreatedAt = r.CreatedAt, DecidedAt = r.DecidedAt, DecisionNote = r.DecisionNote, ResultNote = r.ResultNote,
                CompletedAt = r.CompletedAt, ShortageResolution = r.ShortageResolution, RowVer = r.RowVer,
                Lines = r.RequestLines.OrderBy(l => l.RequestLineId).Select(l => new RequestLineDto(
                    l.RequestLineId, l.MaterialId, l.Material == null ? null : l.Material.MaterialName, l.Material == null ? null : l.Material.Color,
                    l.OrderLineId,
                    l.OrderLine == null ? null : l.OrderLine.ProductVariant.Product.ProductCode + " · " + l.OrderLine.ProductVariant.Color + " · " + l.OrderLine.ProductVariant.SizeCode,
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
        };
    }
}
