using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// MI-10 (UC59): công nhân may khoán sản phẩm (<c>PIECE_RATE</c>) xem sản lượng + lương tạm tính của chính mình.
/// Đọc <c>vw_WorkerDailyEstimatedPay</c> trực tiếp vì <c>IOutputQueryService</c> (KI-10, Kiều) chưa có — DEV-MI-10-1.
/// Hệ thống không tính lương chính thức (GBR-12): mọi con số là tạm tính.
/// </summary>
public interface IWorkerPayService
{
    /// <summary>Các ngày có sản lượng hoặc Scrap trong khoảng + tổng. Người không khoán sản phẩm nhận danh sách rỗng.</summary>
    Task<Result<MyEstimatedPayDto>> GetMyEstimatedPayAsync(int employeeId, MyEstimatedPayQuery query, CancellationToken ct);

    /// <summary>Từng dòng sản lượng theo công đoạn + từng dòng Scrap của một ngày (Scrap đã hủy không tính).</summary>
    Task<Result<MyDayDetailDto>> GetMyDayDetailAsync(int employeeId, DateOnly workDate, CancellationToken ct);
}
