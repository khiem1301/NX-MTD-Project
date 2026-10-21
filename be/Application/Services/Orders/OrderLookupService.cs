using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services.Orders;

/// <summary>Tra cứu cho luồng khác (kho, cắt, may, chấm công): tuyệt đối không có đơn giá (D26).</summary>
public sealed class OrderLookupService : IOrderLookupService
{
    private readonly ICustomerOrderRepository _orders;
    private readonly IProductRepository _products;

    public OrderLookupService(ICustomerOrderRepository orders, IProductRepository products)
    {
        _orders = orders; _products = products;
    }

    public async Task<Result<PagedResult<OrderLookupDto>>> OrdersAsync(OrderLookupQuery query, CancellationToken ct)
    {
        IReadOnlyCollection<string> statuses = OrderStatuses.Running;
        if (!string.IsNullOrWhiteSpace(query.Status)) statuses = new[] { CodeText.ToCode(query.Status) };
        return Result<PagedResult<OrderLookupDto>>.Success(await _orders.LookupAsync(query, statuses, ct));
    }

    public async Task<Result<IReadOnlyList<OrderLineLookupDto>>> LinesAsync(int orderId, CancellationToken ct) =>
        await _orders.ExistsAsync(orderId, ct)
            ? Result<IReadOnlyList<OrderLineLookupDto>>.Success(await _orders.LookupLinesAsync(orderId, ct))
            : Fail.NotFound<IReadOnlyList<OrderLineLookupDto>>();

    public async Task<Result<PagedResult<ProductLookupDto>>> ProductsAsync(PageQuery query, CancellationToken ct) =>
        Result<PagedResult<ProductLookupDto>>.Success(await _products.LookupAsync(query, ct));
}
