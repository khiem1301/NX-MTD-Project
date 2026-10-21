using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services.Requests;
using NX_MTD.Infrastructure.Repositories;

namespace NX_MTD.API.Modules;

public static class RequestModule
{
    public static IServiceCollection AddRequestModule(this IServiceCollection services)
    {
        services.AddScoped<RequestAccess>();
        services.AddScoped<RequestTypeHandlers>();
        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<IRequestService, RequestService>();
        services.AddScoped<IRequestQueryService, RequestQueryService>();
        services.AddScoped<IAttachmentOwnerAccess, RequestAttachmentAccess>();
        return services;
    }
}
