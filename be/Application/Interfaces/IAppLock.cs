namespace NX_MTD.Application.Interfaces;

/// <summary>Khóa độc quyền theo tên trong transaction hiện tại (sp_getapplock); tự nhả khi commit/rollback.</summary>
public interface IAppLock
{
    Task AcquireAsync(string resource, CancellationToken ct);
}
