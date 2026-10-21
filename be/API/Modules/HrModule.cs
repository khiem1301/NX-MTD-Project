using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Hr;
using NX_MTD.Infrastructure.Repositories;
using NX_MTD.Infrastructure.Security;

namespace NX_MTD.API.Modules;

public static class HrModule
{
    public static IServiceCollection AddHrModule(this IServiceCollection services)
    {
        services.AddScoped<EmployeeScope>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IAccountAdminService, AccountAdminService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IHrLookupService, HrLookupService>();
        return services;
    }
}
