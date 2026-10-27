using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>MI-10 (UC59): sản lượng + lương tạm tính của chính công nhân. Luôn lọc theo EmployeeId của người gọi.</summary>
public sealed class WorkerPayService : IWorkerPayService
{
    // Mã DB của DefectRecord (luồng Kiều chưa khai hằng dùng chung — DEV-MI-10-1), khớp vw_WorkerDailyEstimatedPay.
    private const string ScrapType = "SCRAP";
    private const string CancelledStatus = "CANCELLED";

    private readonly IUnitOfWork _uow;
    private readonly IOutputQueryService _output;
    private readonly IClock _clock;

    public WorkerPayService(IUnitOfWork uow, IOutputQueryService output, IClock clock)
    {
        _uow = uow;
        _output = output;
        _clock = clock;
    }

    public async Task<Result<MyEstimatedPayDto>> GetMyEstimatedPayAsync(
        int employeeId, MyEstimatedPayQuery query, CancellationToken ct)
    {
        var today = _clock.Today;
        var from = query.From ?? new DateOnly(today.Year, today.Month, 1);
        var to = query.To ?? today;

        // KI-10: số liệu lương tạm tính đọc qua IOutputQueryService, không tự truy vấn view (DEV-MI-10-1 đã đóng).
        var days = (await _output.GetWorkerDailyPayAsync(employeeId, from, to, ct)).ToList();

        var dto = new MyEstimatedPayDto(from, to, true, days,
            days.Sum(d => d.OutputQty), days.Sum(d => d.ScrapQty), days.Sum(d => d.OutputAmount),
            days.Sum(d => d.SundayBonus), days.Sum(d => d.ScrapAmount), days.Sum(d => d.EstimatedPay));
        return Result<MyEstimatedPayDto>.Success(dto, days.Count == 0 ? Messages.NoData : null);
    }

    public async Task<Result<MyDayDetailDto>> GetMyDayDetailAsync(int employeeId, DateOnly workDate, CancellationToken ct)
    {
        var outputs = await _uow.Repository<OutputRecord>().Query().AsNoTracking()
            .Where(o => o.WorkerId == employeeId && o.WorkDate == workDate)
            .OrderBy(o => o.ShiftCode).ThenBy(o => o.Operation.StationSequence)
            .Select(o => new MyOutputLineDto(o.ShiftCode, o.Operation.OperationCode, o.Operation.OperationName,
                o.OrderLine.Order.OrderCode, o.OrderLine.ProductVariant.Product.ProductCode, o.Quantity, o.PieceRate.Rate,
                o.Quantity * o.PieceRate.Rate))
            .ToListAsync(ct);

        // Khớp vw_WorkerDailyEstimatedPay: chỉ Scrap, bỏ bản ghi đã hủy.
        var scraps = await _uow.Repository<DefectRecord>().Query().AsNoTracking()
            .Where(d => d.ResponsibleWorkerId == employeeId && d.WorkDate == workDate
                        && d.DefectType == ScrapType && d.Status != CancelledStatus)
            .OrderBy(d => d.Operation.StationSequence)
            .Select(d => new MyScrapLineDto(d.Operation.OperationCode, d.Operation.OperationName,
                d.OrderLine.Order.OrderCode, d.OrderLine.ProductVariant.Product.ProductCode, d.Quantity,
                d.PieceRate == null ? 0m : d.PieceRate.Rate, d.Quantity * (d.PieceRate == null ? 0m : d.PieceRate.Rate)))
            .ToListAsync(ct);

        return Result<MyDayDetailDto>.Success(new MyDayDetailDto(workDate, outputs, scraps));
    }
}
