namespace NX_MTD.Application.Interfaces;

public interface ISystemSettingService
{
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct);
}
