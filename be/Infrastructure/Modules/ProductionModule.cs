using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Infrastructure.Jobs;
using NX_MTD.Infrastructure.Services;

namespace NX_MTD.Infrastructure.Modules;

/// <summary>
/// Luồng may + QC (KI-01…KI-11): công đoạn, đơn giá, phân công, sản lượng, QC, hàng hỏng. Minh làm hộ Kiều vì Kiều
/// chưa code — xem DEV-KI-xx trong docs/CODE_DEVIATIONS.md. Đăng ký bằng một dòng trong Program.cs.
/// </summary>
public static class ProductionModule
{
    public static IServiceCollection AddProductionModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IOutputQueryService, OutputQueryService>();   // KI-10
        services.AddScoped<ProductionGuard>();
        services.AddScoped<IPieceRateService, PieceRateService>();
        services.AddSingleton<IExcelOperationParser, ClosedXmlOperationParser>();
        services.AddScoped<IOperationService, OperationService>();
        services.AddScoped<IOperationGroupService, OperationGroupService>();
        services.AddScoped<IOperationAssignmentService, OperationAssignmentService>();
        services.AddScoped<IOrderLineTeamService, OrderLineTeamService>();
        services.AddScoped<IOutputService, OutputService>();
        services.AddScoped<IQcBatchService, QcBatchService>();
        services.AddScoped<IDefectService, DefectService>();
        services.AddScoped<IDefectAutoConfirmService, DefectAutoConfirmService>();
        services.AddHostedService<WorkAllocationAutoRetrieveJob>();   // cuối ngày chưa làm hết → thu hồi, ghi lý do
        services.AddHostedService<DefectAutoConfirmJob>();   // quá 48 giờ không phản đối → CONFIRMED
        services.AddScoped<IEndOfOrderService, EndOfOrderService>();
        services.AddScoped<IRequestTypeHandler, ReplacementCuttingRequestHandler>();   // KI-09
        services.AddScoped<IRequestTypeHandler, DefectiveMaterialRequestHandler>();    // NVL lỗi: duyệt → kiểm tồn → phiếu xin / đơn mua
        return services;
    }
}
