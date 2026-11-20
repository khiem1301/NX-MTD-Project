using Microsoft.Extensions.Options;
using NX_MTD.Application.Common;
using NX_MTD.Application.Services;
using NX_MTD.Application.Interfaces;
using NX_MTD.Infrastructure.Ai;
using NX_MTD.Infrastructure.Excel;
using NX_MTD.Infrastructure.Repositories;
using NX_MTD.Infrastructure.Security;
using NX_MTD.Infrastructure.Storage;

namespace NX_MTD.API.Modules;

public static class HrModule
{
    public static IServiceCollection AddHrModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<EmployeeScope>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();
        services.AddSingleton<IUsernameGenerator, UsernameGenerator>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IAccountAdminService, AccountAdminService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IHrLookupService, HrLookupService>();
        services.AddScoped<IGradeService, GradeService>();
        services.AddSingleton<IExcelEmployeeParser, ClosedXmlEmployeeParser>();   // KH-22
        services.AddScoped<IEmployeeImportService, EmployeeImportService>();
        AddGradeSuggestion(services, config);
        return services;
    }

    /// <summary>DEV-ALL-148: AI gợi ý bậc thợ (Gemini) + kho lưu tạm bản gợi ý.</summary>
    private static void AddGradeSuggestion(IServiceCollection services, IConfiguration config)
    {
        services.Configure<GradeSuggestionOptions>(config.GetSection("GradeSuggestion"));
        services.Configure<GeminiOptions>(config.GetSection("Gemini"));
        services.AddSingleton<IGradeSuggestionStore, LocalGradeSuggestionStore>();
        services.AddScoped<IGradeSuggestionService, GradeSuggestionService>();
        services.AddHttpClient<IGradeAdvisor, GeminiGradeAdvisor>((sp, http) =>
        {
            var gemini = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
            http.BaseAddress = new Uri(gemini.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(gemini.TimeoutSeconds);
        });
    }
}
