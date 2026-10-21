using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Orders;
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
        services.AddScoped<ICustomerOrderService, CustomerOrderService>();
        services.AddScoped<IOrderLookupService, OrderLookupService>();
        return services;
    }
}
