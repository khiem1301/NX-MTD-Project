using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Repositories;

public sealed class CustomerRepository : GenericRepository<Customer>, ICustomerRepository
{
    public CustomerRepository(NxMtdDbContext db) : base(db) { }

    public async Task<IReadOnlyList<CustomerDto>> ListAsync(string? keyword, CancellationToken ct)
    {
        var q = _context.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(c => c.CustomerName.Contains(k) || (c.Phone != null && c.Phone.Contains(k)));
        }
        return await q.OrderBy(c => c.CustomerName)
            .Select(c => new CustomerDto(c.CustomerId, c.CustomerName, c.Phone, c.Email, c.DefaultAddress)).ToListAsync(ct);
    }

    public Task<bool> NameExistsAsync(string name, int? exceptId, CancellationToken ct) =>
        _context.Customers.AnyAsync(c => c.CustomerName == name && (exceptId == null || c.CustomerId != exceptId), ct);
}

public sealed class ProductRepository : GenericRepository<Product>, IProductRepository
{
    public ProductRepository(NxMtdDbContext db) : base(db) { }

    public async Task<PagedResult<ProductListItemDto>> ListAsync(ProductQuery q, string? statusCode, CancellationToken ct)
    {
        var query = _context.Products.AsNoTracking();
        if (statusCode is not null) query = query.Where(p => p.Status == statusCode);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var k = q.Keyword.Trim();
            query = query.Where(p => p.ProductCode.Contains(k) || p.ProductName.Contains(k));
        }
        var page = await query.OrderBy(p => p.ProductCode)
            .Select(p => new { p.ProductId, p.ProductCode, p.ProductName, p.SampleImageUrl, p.Status, IsUsed = p.ProductVariants.Any(v => v.OrderLines.Any()), p.CreatedAt })
            .ToPagedAsync(q, ct);
        var items = page.Items.Select(p => new ProductListItemDto(p.ProductId, p.ProductCode, p.ProductName,
            p.SampleImageUrl, CodeText.ToPascal(p.Status), p.IsUsed, p.CreatedAt)).ToList();
        return new PagedResult<ProductListItemDto>(items, page.Page, page.PageSize, page.TotalItems);
    }

    public async Task<ProductDetailDto?> GetDetailAsync(int productId, CancellationToken ct)
    {
        var p = await _context.Products.AsNoTracking().Where(x => x.ProductId == productId)
            .Select(x => new { x.ProductId, x.ProductCode, x.ProductName, x.SampleImageUrl, x.Description,
                x.Status, IsUsed = x.ProductVariants.Any(v => v.OrderLines.Any()), x.CreatedAt })
            .FirstOrDefaultAsync(ct);
        if (p is null) return null;
        // Tỉ lệ size của bàn cắt gần nhất không còn lưu trên Product mà đọc từ vw_ProductLastSizeRatio.
        var lastSizeRatio = await _context.VwProductLastSizeRatios.AsNoTracking()
            .Where(v => v.ProductId == productId).Select(v => v.SizeRatio).FirstOrDefaultAsync(ct);
        return new ProductDetailDto(p.ProductId, p.ProductCode, p.ProductName, p.SampleImageUrl,
            p.Description, lastSizeRatio, CodeText.ToPascal(p.Status), p.IsUsed, p.CreatedAt, Array.Empty<AuditLogDto>());
    }

    public Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct) =>
        _context.Products.AnyAsync(p => p.ProductCode == code && (exceptId == null || p.ProductId != exceptId), ct);

    public Task<bool> IsUsedAsync(int productId, CancellationToken ct) =>
        _context.OrderLines.AnyAsync(l => l.ProductId == productId, ct);   // OrderLine.ProductId khớp ProductVariant (FK ghép)

    public async Task<IReadOnlyList<string>> FindInactiveCodesAsync(IReadOnlyCollection<int> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();
        var found = await _context.Products.AsNoTracking().Where(p => ids.Contains(p.ProductId))
            .Select(p => new { p.ProductId, p.ProductCode, p.Status }).ToListAsync(ct);
        var inactive = found.Where(p => p.Status != RecordStatus.Active).Select(p => p.ProductCode);
        var missing = ids.Except(found.Select(p => p.ProductId)).Select(id => $"#{id}");
        return inactive.Concat(missing).ToList();
    }

    public async Task<PagedResult<ProductLookupDto>> LookupAsync(PageQuery q, CancellationToken ct)
    {
        var query = _context.Products.AsNoTracking().Where(p => p.Status == RecordStatus.Active);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var k = q.Keyword.Trim();
            query = query.Where(p => p.ProductCode.Contains(k) || p.ProductName.Contains(k));
        }
        return await query.OrderBy(p => p.ProductCode)
            .Select(p => new ProductLookupDto(p.ProductId, p.ProductCode, p.ProductName)).ToPagedAsync(q, ct);
    }
}

public sealed class CustomerOrderRepository : GenericRepository<CustomerOrder>, ICustomerOrderRepository
{
    public CustomerOrderRepository(NxMtdDbContext db) : base(db) { }

    public Task<CustomerOrder?> GetWithLinesAsync(int orderId, CancellationToken ct) =>
        _context.CustomerOrders.Include(o => o.OrderLines).ThenInclude(l => l.ProductVariant)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);

    public Task<bool> CustomerExistsAsync(int customerId, CancellationToken ct) =>
        _context.Customers.AnyAsync(c => c.CustomerId == customerId, ct);

    public Task<bool> CodeExistsAsync(string code, int? exceptId, CancellationToken ct) =>
        _context.CustomerOrders.AnyAsync(o => o.OrderCode == code && (exceptId == null || o.OrderId != exceptId), ct);

    public Task<bool> ExistsAsync(int orderId, CancellationToken ct) =>
        _context.CustomerOrders.AnyAsync(o => o.OrderId == orderId, ct);

    public Task<string?> GetMaxCodeAsync(string prefix, CancellationToken ct) =>
        _context.CustomerOrders.Where(o => o.OrderCode.StartsWith(prefix)).MaxAsync(o => (string?)o.OrderCode, ct);

    public async Task<PagedResult<OrderListItemDto>> ListAsync(OrderQuery q, string? statusCode, CancellationToken ct)
    {
        var query = _context.CustomerOrders.AsNoTracking();
        if (statusCode is not null) query = query.Where(o => o.Status == statusCode);
        if (q.CustomerId is not null) query = query.Where(o => o.CustomerId == q.CustomerId);
        if (q.DueFrom is not null) query = query.Where(o => o.DueDate >= q.DueFrom);
        if (q.DueTo is not null) query = query.Where(o => o.DueDate <= q.DueTo);
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var k = q.Keyword.Trim();
            query = query.Where(o => o.OrderCode.Contains(k) || o.Customer.CustomerName.Contains(k)
                                     || o.OrderLines.Any(l => l.ProductVariant.Product.ProductCode.Contains(k)));
        }

        query = q.SortBy?.ToLowerInvariant() switch
        {
            "ordercode" => q.Desc ? query.OrderByDescending(o => o.OrderCode) : query.OrderBy(o => o.OrderCode),
            "receiveddate" => q.Desc ? query.OrderByDescending(o => o.ReceivedDate) : query.OrderBy(o => o.ReceivedDate),
            _ => q.Desc ? query.OrderByDescending(o => o.DueDate).ThenByDescending(o => o.OrderId) : query.OrderBy(o => o.DueDate).ThenBy(o => o.OrderId),
        };

        var page = await query.Select(o => new
        {
            o.OrderId, o.OrderCode, o.Customer.CustomerName, o.ReceivedDate, o.DueDate, o.Status, o.MaterialStatus, IsDelayRisk = o.IsDelayRisk ?? false,
            TotalQty = o.OrderLines.Sum(l => (int?)l.OrderedQty) ?? 0,
            TotalAmount = o.OrderLines.Sum(l => (decimal?)(l.OrderedQty * l.CmtUnitPrice)) ?? 0m,
        }).ToPagedAsync(q, ct);

        // Mã hàng của từng đơn lấy bằng 1 query phụ (string.Join trong Select không chắc dịch được sang SQL).
        var ids = page.Items.Select(o => o.OrderId).ToList();
        var codes = (await _context.OrderLines.AsNoTracking().Where(l => ids.Contains(l.OrderId))
                .Select(l => new { l.OrderId, l.ProductVariant.Product.ProductCode }).Distinct().ToListAsync(ct))
            .GroupBy(x => x.OrderId).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.ProductCode).OrderBy(c => c)));

        var items = page.Items.Select(o => new OrderListItemDto(o.OrderId, o.OrderCode, o.CustomerName, o.ReceivedDate,
            o.DueDate, CodeText.ToPascal(o.Status), CodeText.ToPascal(o.MaterialStatus), o.IsDelayRisk, o.TotalQty,
            o.TotalAmount, codes.GetValueOrDefault(o.OrderId, string.Empty))).ToList();
        return new PagedResult<OrderListItemDto>(items, page.Page, page.PageSize, page.TotalItems);
    }

    public async Task<OrderDetailDto?> GetDetailAsync(int orderId, CancellationToken ct)
    {
        var dto = await _context.CustomerOrders.AsNoTracking().Where(o => o.OrderId == orderId)
            .Select(o => new OrderDetailDto
            {
                OrderId = o.OrderId, OrderCode = o.OrderCode, CustomerId = o.CustomerId, CustomerName = o.Customer.CustomerName,
                ReceivedDate = o.ReceivedDate, DueDate = o.DueDate, Status = o.Status, MaterialStatus = o.MaterialStatus,
                IsDelayRisk = o.IsDelayRisk ?? false, StopReason = o.StopReason, Note = o.Note, CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt, RowVer = o.RowVer,
                Lines = o.OrderLines.OrderBy(l => l.ProductVariant.Product.ProductCode).ThenBy(l => l.ProductVariant.Color).ThenBy(l => l.ProductVariant.SizeCode)
                    .Select(l => new OrderLineDto(l.OrderLineId, l.ProductId, l.ProductVariant.Product.ProductCode,
                        l.ProductVariant.Product.ProductName, l.ProductVariant.Color, l.ProductVariant.SizeCode,
                        l.OrderedQty, l.CmtUnitPrice)).ToList(),
            }).FirstOrDefaultAsync(ct);
        return dto is null ? null : dto with
        {
            Status = CodeText.ToPascal(dto.Status),
            MaterialStatus = CodeText.ToPascal(dto.MaterialStatus),
        };
    }

    public async Task<PagedResult<OrderLookupDto>> LookupAsync(OrderLookupQuery q, IReadOnlyCollection<string> statuses, CancellationToken ct)
    {
        var query = _context.CustomerOrders.AsNoTracking().Where(o => statuses.Contains(o.Status));
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var k = q.Keyword.Trim();
            query = query.Where(o => o.OrderCode.Contains(k) || o.Customer.CustomerName.Contains(k));
        }
        var page = await query.OrderBy(o => o.DueDate).ThenBy(o => o.OrderId)
            .Select(o => new { o.OrderId, o.OrderCode, o.Customer.CustomerName, o.DueDate, o.Status, o.MaterialStatus })
            .ToPagedAsync(q, ct);
        var items = page.Items.Select(o => new OrderLookupDto(o.OrderId, o.OrderCode, o.CustomerName, o.DueDate,
            CodeText.ToPascal(o.Status), CodeText.ToPascal(o.MaterialStatus))).ToList();
        return new PagedResult<OrderLookupDto>(items, page.Page, page.PageSize, page.TotalItems);
    }

    /// <summary>Không chọn CmtUnitPrice (D26).</summary>
    public async Task<IReadOnlyList<OrderLineLookupDto>> LookupLinesAsync(int orderId, CancellationToken ct) =>
        await _context.OrderLines.AsNoTracking().Where(l => l.OrderId == orderId)
            .OrderBy(l => l.ProductVariant.Product.ProductCode).ThenBy(l => l.ProductVariant.Color).ThenBy(l => l.ProductVariant.SizeCode)
            .Select(l => new OrderLineLookupDto(l.OrderLineId, l.OrderId, l.ProductId, l.ProductVariant.Product.ProductCode,
                l.ProductVariant.Product.ProductName, l.ProductVariant.Color, l.ProductVariant.SizeCode, l.OrderedQty)).ToListAsync(ct);
}
