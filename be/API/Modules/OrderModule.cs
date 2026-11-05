using NX_MTD.Application.Services;
using NX_MTD.Application.Interfaces;
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
        services.AddScoped<IProductImageCleanupService, ProductImageCleanupService>();
        services.AddScoped<ICustomerOrderService, CustomerOrderService>();
        services.AddScoped<IOrderLookupService, OrderLookupService>();
        services.AddScoped<IOrderProgressService, OrderProgressService>();
        return services;
    }
}
