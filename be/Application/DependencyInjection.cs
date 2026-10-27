using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;

namespace NX_MTD.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();

        // Kho, cắt (Thái)
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IMaterialService, MaterialService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IOrderMaterialService, OrderMaterialService>();
        services.AddScoped<IIncomingMaterialService, IncomingMaterialService>();   // màn NVL chờ nhận của Thủ kho
        services.AddScoped<MaterialSupplyUpdater>();
        services.AddScoped<IMaterialReceiptService, MaterialReceiptService>();
        services.AddScoped<IShortDeliveryService, ShortDeliveryService>();
        services.AddScoped<IRequestTypeHandler, ShortDeliveryRequestHandler>();
        services.AddScoped<IAttachmentOwnerAccess, MaterialReceiptAttachmentAccess>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<FabricPlanner>();
        services.AddScoped<IWorkOrderService, WorkOrderService>();
        services.AddScoped<IMaterialIssueService, MaterialIssueService>();
        services.AddScoped<ICuttingLayService, CuttingLayService>();
        services.AddScoped<ICutHandoverService, CutHandoverService>();
        services.AddScoped<IFgHandoverService, FgHandoverService>();
        services.AddScoped<IDispatchNoteService, DispatchNoteService>();
        services.AddScoped<IWarehouseDocumentExportService, WarehouseDocumentExportService>();   // TH-16
        services.AddScoped<IWorkAllocationService, WorkAllocationService>();
        services.AddScoped<IMaterialReturnService, MaterialReturnService>();
        services.AddScoped<IStocktakeService, StocktakeService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<INotificationInboxService, NotificationInboxService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IGradeService, GradeService>();
        services.AddScoped<IOrderProgressService, OrderProgressService>();
        services.AddScoped<IEmployeeImportService, NX_MTD.Application.Services.Hr.EmployeeImportService>();
        services.AddScoped<IAuditLogQueryService, NX_MTD.Application.Services.Hr.AuditLogQueryService>();
        services.AddScoped<IAttachmentOwnerAccess, VehicleTripAttachmentAccess>();
        services.AddScoped<IAttachmentOwnerAccess, DispatchNoteAttachmentAccess>();
        services.AddValidatorsFromAssemblyContaining<LoginDtoValidator>();
        return services;
    }
}
