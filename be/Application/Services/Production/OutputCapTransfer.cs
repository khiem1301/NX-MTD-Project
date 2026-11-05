using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-64: cuối ca công nhân báo vượt số được giao ở một công đoạn (hai người chia nhau công đoạn và làm lệch so với lúc giao).
/// Phần vượt lấy từ người cùng tổ, cùng công đoạn × dòng đơn còn dư (dư nhiều lấy trước), thiếu nữa thì lấy phần tổ chưa giao.
/// Ghi thu hồi ADJUSTED từ người dư rồi giao cho người báo vượt (tổ trưởng ghi, CONFIRMED ngay), trước khi ghi sản lượng.
/// </summary>
internal sealed class OutputCapTransfer
{
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public OutputCapTransfer(IUnitOfWork uow, IClock clock)
    {
        _uow = uow; _clock = clock;
    }

    /// <summary>Phần báo vượt số được giao của một công nhân ở một công đoạn.</summary>
    public sealed record Shortage(int OperationId, int WorkerId, int Quantity);

    public sealed record Context(int TeamId, int OrderLineId, DateOnly WorkDate, string ShiftCode);

    /// <summary>Số còn dư của từng công nhân trong tổ (được giao − đã báo) và phần tổ chưa giao, theo công đoạn.</summary>
    public sealed record Sources(Dictionary<(int OperationId, int WorkerId), int> Spares, Dictionary<int, int> Pool)
    {
        /// <summary>Chuyển được tối đa cho công nhân ở công đoạn: phần dư của người khác cộng phần tổ chưa giao.</summary>
        public int TransferableTo(int operationId, int workerId) =>
            Spares.Where(s => s.Key.OperationId == operationId && s.Key.WorkerId != workerId).Sum(s => s.Value)
            + Pool.GetValueOrDefault(operationId);
    }

    private sealed record Move(Shortage To, int? FromWorkerId, int Quantity);

    public async Task<Sources> LoadSourcesAsync(int teamId, int orderLineId, IReadOnlyCollection<int> teamWorkerIds, CancellationToken ct)
    {
        var caps = await _uow.Repository<VwWorkerOutputCap>().Query().AsNoTracking()
            .Where(v => v.OrderLineId == orderLineId && teamWorkerIds.Contains(v.WorkerId))
            .Select(v => new { v.OperationId, v.WorkerId, Spare = (v.CapQty ?? 0) - v.OutputQty }).ToListAsync(ct);
        var pool = await _uow.Repository<VwOperationAllocationProgress>().Query().AsNoTracking()
            .Where(v => v.SewingTeamId == teamId && v.OrderLineId == orderLineId)
            .Select(v => new { v.OperationId, Qty = v.UnallocatedQty ?? 0 }).ToListAsync(ct);
        return new Sources(caps.Where(c => c.Spare > 0).ToDictionary(c => (c.OperationId, c.WorkerId), c => c.Spare),
            pool.Where(p => p.Qty > 0).ToDictionary(p => p.OperationId, p => p.Qty));
    }

    /// <summary>
    /// Ghi phiếu chuyển cho các phần báo vượt. <paramref name="outputIncrease"/>: phần sản lượng tăng thêm trong lần lưu này của từng
    /// công nhân × công đoạn, trừ khỏi phần dư để không lấy của người đang báo thêm. Lưu thu hồi trước, giao sau, để trigger 50050 / 50052
    /// thấy đúng trần ở từng bước. Trả các dòng thông báo đã chuyển; thiếu nguồn thì lỗi OUTPUT_EXCEEDS_CAP.
    /// </summary>
    public async Task<Result<List<string>>> ApplyAsync(Context ctx, IReadOnlyList<Shortage> shortages,
        IReadOnlyDictionary<(int OperationId, int WorkerId), int> outputIncrease, IReadOnlyDictionary<int, Operation> operations,
        CancellationToken ct)
    {
        var workers = await _uow.Repository<Employee>().Query().AsNoTracking().Where(e => e.TeamId == ctx.TeamId)
            .Select(e => new { e.EmployeeId, e.FullName }).ToDictionaryAsync(e => e.EmployeeId, e => e.FullName, ct);
        var sources = await LoadSourcesAsync(ctx.TeamId, ctx.OrderLineId, workers.Keys.ToList(), ct);
        foreach (var (key, increase) in outputIncrease.Where(o => o.Value > 0 && sources.Spares.ContainsKey(o.Key)))
            sources.Spares[key] = Math.Max(0, sources.Spares[key] - increase);

        var moves = new List<Move>();
        foreach (var s in shortages)
        {
            var available = sources.TransferableTo(s.OperationId, s.WorkerId);
            if (available < s.Quantity)
                return Result<List<string>>.Failure(ErrorCodes.OutputExceedsCap, string.Format(Messages.OutputTransferNotEnough,
                    operations[s.OperationId].OperationCode, s.Quantity, available));
            moves.AddRange(Take(sources, s));
        }

        string NameOf(int id) => workers.GetValueOrDefault(id) ?? id.ToString();
        foreach (var m in moves.Where(m => m.FromWorkerId is not null))
            await AddRowAsync(ctx, m.FromWorkerId!.Value, m.To.OperationId, PieceMovementTypes.Retrieval, m.Quantity,
                string.Format(Messages.OutputTransferNote, NameOf(m.To.WorkerId), ctx.ShiftCode, ctx.WorkDate.ToString("dd/MM/yyyy")));
        await _uow.SaveChangesAsync();
        foreach (var s in shortages)
            await AddRowAsync(ctx, s.WorkerId, s.OperationId, PieceMovementTypes.Handover, s.Quantity, null);
        await _uow.SaveChangesAsync();

        return Result<List<string>>.Success(moves.Select(m => string.Format(Messages.OutputTransferDone, m.Quantity,
            operations[m.To.OperationId].OperationCode,
            m.FromWorkerId is { } from ? NameOf(from) : Messages.OutputTransferFromPool, NameOf(m.To.WorkerId))).ToList());
    }

    /// <summary>Lấy phần dư của người cùng công đoạn (dư nhiều lấy trước), thiếu thì lấy phần tổ chưa giao; trừ dần vào nguồn.</summary>
    private static List<Move> Take(Sources sources, Shortage s)
    {
        var need = s.Quantity;
        var moves = new List<Move>();
        var donors = sources.Spares.Where(x => x.Key.OperationId == s.OperationId && x.Key.WorkerId != s.WorkerId && x.Value > 0)
            .OrderByDescending(x => x.Value).ThenBy(x => x.Key.WorkerId).ToList();
        foreach (var (key, spare) in donors)
        {
            if (need == 0) break;
            var qty = Math.Min(need, spare);
            sources.Spares[key] = spare - qty;
            need -= qty;
            moves.Add(new Move(s, key.WorkerId, qty));
        }
        if (need > 0)
        {
            sources.Pool[s.OperationId] -= need;
            moves.Add(new Move(s, null, need));
        }
        return moves;
    }

    private Task AddRowAsync(Context ctx, int workerId, int operationId, string type, int qty, string? note) =>
        _uow.Repository<WorkAllocation>().AddAsync(new WorkAllocation
        {
            WorkDate = ctx.WorkDate,
            MovementType = type,
            SewingTeamId = ctx.TeamId,
            WorkerId = workerId,
            OrderLineId = ctx.OrderLineId,
            OperationId = operationId,
            Quantity = qty,
            ConfirmedQty = qty,
            RetrievalReason = type == PieceMovementTypes.Retrieval ? RetrievalReasons.Adjusted : null,
            RetrievalNote = note,
            ConfirmMode = ConfirmModes.Leader,
            Status = HandoverStatuses.Confirmed,
            CreatedAt = _clock.Now,
            ConfirmedAt = _clock.Now,
        });
}
