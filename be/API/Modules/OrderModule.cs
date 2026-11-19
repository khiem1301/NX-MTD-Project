using NX_MTD.Application.Services;
using NX_MTD.Application.Interfaces;
using NX_MTD.Infrastructure.Excel;
using NX_MTD.Infrastructure.Repositories;

namespace NX_MTD.API.Modules;

public static class OrderModule
{
    public static IServiceCollection AddOrderModule(this IServiceCollection services)
    {
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICustomerOrderRepository, CustomerOrderRepository>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductSpecService, ProductSpecService>();
        services.AddScoped<IOrderMarkerService, OrderMarkerService>();
        services.AddSingleton<IExcelSpecWorkbookParser, ClosedXmlSpecWorkbookParser>();
        services.AddScoped<ISpecImportService, SpecImportService>();
        services.AddSingleton<IExcelOrderLinesParser, ClosedXmlOrderLinesParser>();
        services.AddScoped<IOrderImportService, OrderImportService>();
        services.AddSingleton<IExcelProductParser, ClosedXmlProductParser>();
        services.AddScoped<IProductImportService, ProductImportService>();
        services.AddScoped<IExcelBundleService, ExcelBundleService>();
        services.AddSingleton<IImportTemplateWriter, ClosedXmlImportTemplateWriter>();
        services.AddScoped<IImportTemplateService, ImportTemplateService>();
        services.AddScoped<IProductImageCleanupService, ProductImageCleanupService>();
        services.AddScoped<ICustomerOrderService, CustomerOrderService>();
        services.AddScoped<IOrderLookupService, OrderLookupService>();
        services.AddScoped<IOrderProgressService, OrderProgressService>();
        return services;
    }
}
