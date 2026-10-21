using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services.Requests;

public sealed class RequestAttachmentAccess : IAttachmentOwnerAccess
{
    private const int DefaultMaxPhotos = 5;

    private readonly IRequestRepository _requests;
    private readonly RequestAccess _access;
    private readonly ISystemSettingService _settings;

    public RequestAttachmentAccess(IRequestRepository requests, RequestAccess access, ISystemSettingService settings)
    {
        _requests = requests; _access = access; _settings = settings;
    }

    public string OwnerType => AttachmentOwnerTypes.Request;

    public async Task<bool> CanReadAsync(int ownerId, CancellationToken ct)
    {
        var r = await _requests.GetHeaderAsync(ownerId, ct);
        return r is not null && _access.CanView(r.RequestType, r.CreatedBy, r.CreatedByTeamId);
    }

    /// <summary>Chỉ phía người tạo, khi đơn còn chờ (UC07).</summary>
    public async Task<bool> CanWriteAsync(int ownerId, CancellationToken ct)
    {
        var r = await _requests.GetHeaderAsync(ownerId, ct);
        return r is not null && _access.IsCreatorSide(r.CreatedBy, r.CreatedByTeamId) && RequestStatuses.Pending.Contains(r.Status);
    }

    public Task<int> MaxFilesAsync(CancellationToken ct) => _settings.GetIntAsync(SettingKeys.RequestMaxPhotos, DefaultMaxPhotos, ct);
}
