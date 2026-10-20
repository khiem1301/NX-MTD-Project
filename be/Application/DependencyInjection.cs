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
        services.AddValidatorsFromAssemblyContaining<LoginDtoValidator>();
        return services;
    }
}
