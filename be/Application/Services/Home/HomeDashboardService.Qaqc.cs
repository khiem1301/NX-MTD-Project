using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed partial class HomeDashboardService
{
    /// <summary>
    /// QA/QC: hàng trên chuyền chưa kiểm (đã cấp tổ may − hoàn thiện − hủy), hoàn thiện chưa nhập kho thành phẩm và các phiếu
    /// bàn giao thành phẩm đang chờ kho nhận. Chỉ tính đơn đang sản xuất / giao dở.
    /// </summary>
    public async Task<Result<QaqcHomeDto>> GetQaqcAsync(CancellationToken ct)
    {
        var running = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .Where(o => o.Status == OrderStatuses.InProduction || o.Status == OrderStatuses.InDelivery
                        || o.Status == OrderStatuses.PartiallyDelivered)
            .Select(o => o.OrderId).ToListAsync(ct);
        var onLineQty = 0;
        var notReceivedQty = 0;
        var items = new List<HomeItemDto>();
        try
        {
            var lines = await _uow.Repository<VwOrderLineProgress>().Query().AsNoTracking()
                .Where(v => running.Contains(v.OrderId))
                .Select(v => new
                {
                    v.OrderCode,
                    v.ProductCode,
                    v.Color,
                    v.SizeCode,
                    v.IssuedToSewingQty,
                    v.FinishedQty,
                    v.FinishedReceivedQty,
                    v.ScrapQty
                }).ToListAsync(ct);
            var onLine = lines.Select(l => new { l, Qty = Math.Max(0, l.IssuedToSewingQty - l.FinishedQty - l.ScrapQty) })
                .Where(x => x.Qty > 0).OrderByDescending(x => x.Qty).ToList();
            onLineQty = onLine.Sum(x => x.Qty);
            notReceivedQty = lines.Sum(l => Math.Max(0, l.FinishedQty - l.FinishedReceivedQty));
            items = onLine.Take(LatestCount).Select(x => new HomeItemDto(
                $"{x.l.OrderCode} · {x.l.ProductCode} · {x.l.Color} / {x.l.SizeCode} · {x.Qty} sp", null, "Chờ kiểm", "orange")).ToList();
        }
        catch (Exception)
        {
            onLineQty = 0;
            notReceivedQty = 0;
            items = [];
        }
        var handovers = _uow.Repository<FgHandover>().Query().AsNoTracking().Where(h => h.Status == HandoverStatuses.Pending);
        var pending = await handovers.OrderByDescending(h => h.FgHandoverId).Take(LatestCount)
            .Select(h => new { h.HandoverCode, Order = h.Order.OrderCode }).ToListAsync(ct);
        return Result<QaqcHomeDto>.Success(new QaqcHomeDto(
            onLineQty, notReceivedQty, await handovers.CountAsync(ct),
            items, pending.Select(h => new HomeItemDto(h.HandoverCode, h.Order, "Chờ nhập kho", "blue")).ToList()));
    }
}
