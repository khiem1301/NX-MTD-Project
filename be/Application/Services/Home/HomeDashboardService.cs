using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-170: trang chủ theo vai trò. Chỉ đọc; dùng lại service có sẵn (tiến độ đơn, tổng hợp chi phí, hộp đơn từ) để số
/// trên trang chủ khớp với màn nghiệp vụ. Mỗi role một file partial.
/// </summary>
public sealed partial class HomeDashboardService : IHomeDashboardService
{
    /// <summary>Số đơn từ mới nhất hiện trên trang chủ.</summary>
    private const int LatestCount = 5;

    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ICurrentUser _user;
    private readonly IOrderProgressService _progress;
    private readonly ICostSummaryService _costs;
    private readonly IFinanceRepository _finance;
    private readonly IRequestQueryService _requests;
    private readonly IAttendanceService _attendance;
    private readonly IIncomingMaterialService _incoming;
    private readonly IWorkerPayService _pay;
    private readonly IMealService _meals;

    public HomeDashboardService(IUnitOfWork uow, IClock clock, ICurrentUser user, IOrderProgressService progress,
        ICostSummaryService costs, IFinanceRepository finance, IRequestQueryService requests, IAttendanceService attendance,
        IIncomingMaterialService incoming, IWorkerPayService pay, IMealService meals)
    {
        _uow = uow; _clock = clock; _user = user; _progress = progress; _costs = costs; _finance = finance; _requests = requests;
        _attendance = attendance; _incoming = incoming; _pay = pay; _meals = meals;
    }

    /// <summary>Đơn đang sản xuất / đang giao / giao một phần (không gồm đơn mới chờ NVL). PascalCase, so khớp không phân biệt hoa thường.</summary>
    private static readonly HashSet<string> RunningStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        CodeText.ToPascal(OrderStatuses.InProduction),
        CodeText.ToPascal(OrderStatuses.InDelivery),
        CodeText.ToPascal(OrderStatuses.PartiallyDelivered),
    };

    /// <summary>Chấp nhận cả mã DB (IN_PRODUCTION) lẫn PascalCase (InProduction).</summary>
    private static bool IsRunningStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var normalized = status.Contains('_') ? CodeText.ToPascal(status) : status;
        return RunningStatuses.Contains(normalized);
    }

    private (string This, string Last) Months()
    {
        var today = DateOnly.FromDateTime(_clock.Now);
        return (today.ToString("yyyy-MM"), today.AddMonths(-1).ToString("yyyy-MM"));
    }

    /// <summary>Tiến độ các đơn còn mở; lỗi thì trả danh sách rỗng để trang chủ vẫn hiện các ô khác.</summary>
    private async Task<IReadOnlyList<OrderProgressDto>> OpenOrdersAsync(CancellationToken ct)
    {
        var result = await _progress.GetOverviewAsync(includeClosed: false, ct);
        return result.IsSuccess ? result.Data! : [];
    }
}
