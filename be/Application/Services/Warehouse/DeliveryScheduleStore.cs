using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Loại chủ lịch giao: mỗi loại ứng với một cột khoá ngoại của <see cref="DeliveryScheduleDate"/>.</summary>
public enum DeliveryOwner
{
    PurchaseOrder,
    CustomerOrder,
    Request,
}

/// <summary>
/// DEV-ALL-113: lưu và đọc ngày dự kiến giao từng lần (số lần giao = số ngày) trong bảng <c>DeliveryScheduleDate</c>. Cột ngày đầu / ngày
/// cuối trên đơn vẫn được giữ làm tóm tắt (ngày đầu; ngày cuối khi giao từ 2 lần) để các màn lọc / báo chậm không phải đọc cả danh sách.
/// </summary>
public sealed class DeliveryScheduleStore
{
    private readonly IUnitOfWork _uow;

    public DeliveryScheduleStore(IUnitOfWork uow) => _uow = uow;

    private IQueryable<DeliveryScheduleDate> Rows => _uow.Repository<DeliveryScheduleDate>().Query();

    /// <summary>Ngày đầu và ngày cuối (null khi chỉ giao một lần) để ghi vào cột tóm tắt của chủ lịch.</summary>
    public static (DateOnly? From, DateOnly? To) Summary(IReadOnlyList<DateOnly> dates) =>
        dates.Count == 0 ? (null, null) : (dates[0], dates.Count >= 2 ? dates[^1] : null);

    private static IQueryable<DeliveryScheduleDate> Of(IQueryable<DeliveryScheduleDate> rows, DeliveryOwner owner, int[] ids) => owner switch
    {
        DeliveryOwner.PurchaseOrder => rows.Where(r => r.PurchaseOrderId != null && ids.Contains(r.PurchaseOrderId.Value)),
        DeliveryOwner.CustomerOrder => rows.Where(r => r.OrderId != null && ids.Contains(r.OrderId.Value)),
        _ => rows.Where(r => r.RequestId != null && ids.Contains(r.RequestId.Value)),
    };

    private static int OwnerIdOf(DeliveryScheduleDate row, DeliveryOwner owner) => owner switch
    {
        DeliveryOwner.PurchaseOrder => row.PurchaseOrderId!.Value,
        DeliveryOwner.CustomerOrder => row.OrderId!.Value,
        _ => row.RequestId!.Value,
    };

    private static DeliveryScheduleDate NewRow(DeliveryOwner owner, int ownerId, short seq, DateOnly date)
    {
        var row = new DeliveryScheduleDate { SeqNo = seq, DeliveryDate = date };
        switch (owner)
        {
            case DeliveryOwner.PurchaseOrder: row.PurchaseOrderId = ownerId; break;
            case DeliveryOwner.CustomerOrder: row.OrderId = ownerId; break;
            default: row.RequestId = ownerId; break;
        }
        return row;
    }

    /// <summary>Thay lịch của chủ lịch bằng danh sách mới (sửa tại chỗ theo lần giao, thêm / bỏ phần chênh); chưa SaveChanges.</summary>
    public async Task ReplaceAsync(DeliveryOwner owner, int ownerId, IReadOnlyList<DateOnly> dates, CancellationToken ct)
    {
        var repo = _uow.Repository<DeliveryScheduleDate>();
        var existing = await Of(Rows, owner, [ownerId]).ToListAsync(ct);
        foreach (var row in existing.Where(r => r.SeqNo > dates.Count)) repo.Remove(row);
        for (var i = 0; i < dates.Count; i++)
        {
            var seq = (short)(i + 1);
            var row = existing.FirstOrDefault(r => r.SeqNo == seq);
            if (row is null) await repo.AddAsync(NewRow(owner, ownerId, seq, dates[i]));
            else row.DeliveryDate = dates[i];
        }
    }

    public async Task<IReadOnlyList<DateOnly>> GetAsync(DeliveryOwner owner, int ownerId, CancellationToken ct) =>
        await Of(Rows.AsNoTracking(), owner, [ownerId]).OrderBy(r => r.SeqNo).Select(r => r.DeliveryDate).ToListAsync(ct);

    /// <summary>Lịch của nhiều chủ lịch một lần (chủ lịch chưa có ngày nào thì không có khoá).</summary>
    public async Task<Dictionary<int, IReadOnlyList<DateOnly>>> GetManyAsync(DeliveryOwner owner, IReadOnlyCollection<int> ownerIds,
        CancellationToken ct)
    {
        var rows = await Of(Rows.AsNoTracking(), owner, ownerIds.ToArray()).OrderBy(r => r.SeqNo).ToListAsync(ct);
        return rows.GroupBy(r => OwnerIdOf(r, owner))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DateOnly>)g.Select(r => r.DeliveryDate).ToList());
    }
}
