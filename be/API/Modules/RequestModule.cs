using NX_MTD.Application.Services;
using NX_MTD.Application.Interfaces;
using NX_MTD.Infrastructure.Repositories;

namespace NX_MTD.API.Modules;

public static class RequestModule
{
    public static IServiceCollection AddRequestModule(this IServiceCollection services)
    {
        services.AddScoped<RequestAccess>();
        services.AddScoped<RequestTypeHandlers>();
        services.AddScoped<IRequestTypeHandler, SparePartsRequestHandler>();       // Cơ điện xin linh kiện: số nguyên, có trần
        services.AddScoped<IRequestTypeHandler, MachineSuppliesRequestHandler>();  // Cơ điện xin vật tư máy
        services.AddScoped<IRequestTypeHandler, NewMachineRequestHandler>();       // Cơ điện xin máy mới
        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<IRequestService, RequestService>();
        services.AddScoped<IRequestQueryService, RequestQueryService>();
        services.AddScoped<IAttachmentOwnerAccess, RequestAttachmentAccess>();
        return services;
    }
}
