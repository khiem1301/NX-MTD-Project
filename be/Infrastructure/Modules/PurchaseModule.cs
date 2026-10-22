using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Purchasing;

namespace NX_MTD.Infrastructure.Modules;

/// <summary>MI-07 — Nhà cung cấp + đơn mua (chốt đặt hàng) + thanh toán (Minh). Đăng ký bằng một dòng trong Program.cs.</summary>
public static class PurchaseModule
{
    public static IServiceCollection AddPurchaseModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseOrderFlowService, PurchaseOrderFlowService>();
        // Ảnh chứng từ thanh toán (KH-07): quyền theo OwnerType PURCHASE_ORDER.
        services.AddScoped<IAttachmentOwnerAccess, PurchaseOrderAttachmentAccess>();
        return services;
    }
}
