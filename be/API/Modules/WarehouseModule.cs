using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Infrastructure.Excel;
using NX_MTD.Infrastructure.Jobs;
using NX_MTD.Infrastructure.Optimization;
using NX_MTD.Infrastructure.Repositories;

namespace NX_MTD.API.Modules;

/// <summary>Kho, cắt, giao BTP cho công nhân (Thái); kiểm kê, sổ xe (Thảo). Đăng ký bằng một dòng trong Program.cs.</summary>
public static class WarehouseModule
{
    public static IServiceCollection AddWarehouseModule(this IServiceCollection services)
    {
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IMaterialService, MaterialService>();
        services.AddScoped<IOrderMaterialService, OrderMaterialService>();
        services.AddScoped<IIncomingMaterialService, IncomingMaterialService>();   // màn NVL chờ nhận của Thủ kho
        services.AddScoped<MaterialSupplyUpdater>();
        services.AddScoped<DeliveryCountGuard>();   // số phiếu nhập không vượt số lần giao (DEV-ALL-94)
        services.AddScoped<ShortageTracker>();       // thiếu NVL theo cả đơn, hàng bù theo dòng đơn thiếu (DEV-ALL-95)
        services.AddScoped<ICustomerDeliveryPlanService, CustomerDeliveryPlanService>();   // lịch khách giao NVL của đơn (DEV-ALL-95)
        services.AddScoped<IMaterialReceiptService, MaterialReceiptService>();
        services.AddScoped<IShortDeliveryService, ShortDeliveryService>();
        services.AddScoped<IRequestTypeHandler, ShortDeliveryRequestHandler>();
        services.AddScoped<IRequestTypeHandler, MaterialRequisitionRequestHandler>();        // Xin NVL (DEV-ALL-77)
        services.AddScoped<IRequestTypeHandler, SemiFinishedRequisitionRequestHandler>();    // Xin BTP (DEV-ALL-77)
        services.AddScoped<IAttachmentOwnerAccess, MaterialReceiptAttachmentAccess>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IMaterialIssueService, MaterialIssueService>();
        services.AddScoped<MaterialReturnCapacity>();
        services.AddScoped<IMaterialReturnService, MaterialReturnService>();
        services.AddScoped<IFgHandoverService, FgHandoverService>();
        services.AddScoped<IDispatchNoteService, DispatchNoteService>();
        services.AddScoped<IAttachmentOwnerAccess, DispatchNoteAttachmentAccess>();
        services.AddSingleton<IWarehouseDocumentExcelWriter, ClosedXmlWarehouseDocumentWriter>();   // TH-16
        services.AddScoped<IWarehouseDocumentExportService, WarehouseDocumentExportService>();   // TH-16

        // Cắt
        services.AddScoped<FabricPlanner>();
        services.AddSingleton<IMarkerPlanSolver, OrToolsMarkerPlanSolver>();   // ghép bìa ít mét nhất (DEV-ALL-88)
        services.AddScoped<WorkOrderCompletion>();
        services.AddScoped<WorkOrderFabricChecker>();
        services.AddScoped<IWorkOrderFabricNeeds, WorkOrderFabricNeeds>();
        services.AddScoped<IWorkOrderService, WorkOrderService>();
        services.AddScoped<ICuttingLayService, CuttingLayService>();
        services.AddScoped<ICutHandoverService, CutHandoverService>();

        // Giao / thu hồi BTP với công nhân
        services.AddScoped<IWorkAllocationService, WorkAllocationService>();
        services.AddScoped<IDailyWorkAllocationService, DailyWorkAllocationService>();
        services.AddScoped<IWorkAllocationAutoConfirmService, WorkAllocationAutoConfirmService>();
        services.AddHostedService<WorkAllocationAutoConfirmJob>();   // phiếu giao chờ quá 60 phút → hệ thống tự xác nhận (DEV-ALL-101)

        // Kiểm kê, sổ xe (Thảo)
        services.AddScoped<IStocktakeService, StocktakeService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IAttachmentOwnerAccess, VehicleTripAttachmentAccess>();
        return services;
    }
}
