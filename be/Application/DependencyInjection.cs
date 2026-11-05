using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;

namespace NX_MTD.Application;

/// <summary>Phần dùng chung của lớp Application. Service theo luồng đăng ký trong API/Modules.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddValidatorsFromAssemblyContaining<LoginDtoValidator>();
        return services;
    }
}
