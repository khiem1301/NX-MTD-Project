using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Services;

/// <summary>
/// Lưu file bảng lương gốc trong thư mục cục bộ (mặc định <c>App_Data/payroll-files</c>, đổi bằng
/// <c>PayrollFiles:RootPath</c>). Thư mục này không được phục vụ như file tĩnh — chỉ tải qua endpoint có phân quyền.
/// Tên file do hệ thống sinh (GUID), không dùng tên file người dùng tải lên.
/// </summary>
public sealed class LocalPayrollFileStore : IPayrollFileStore
{
    private readonly string _root;

    public LocalPayrollFileStore(IConfiguration config, IHostEnvironment env)
    {
        var configured = config["PayrollFiles:RootPath"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "App_Data", "payroll-files")
            : configured);
    }

    public async Task<string> SaveAsync(string periodMonth, byte[] content, CancellationToken ct)
    {
        var relative = Path.Combine(periodMonth, $"{Guid.NewGuid():N}.xlsx");
        var full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, content, ct);
        return relative.Replace('\\', '/');
    }

    public async Task<byte[]?> ReadAsync(string storedPath, CancellationToken ct)
    {
        var full = Resolve(storedPath);
        return full is not null && File.Exists(full) ? await File.ReadAllBytesAsync(full, ct) : null;
    }

    public Task DeleteAsync(string storedPath, CancellationToken ct)
    {
        var full = Resolve(storedPath);
        if (full is not null && File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    /// <summary>Đường dẫn tuyệt đối nếu nằm trong kho, ngược lại null (chặn "../" thoát khỏi thư mục kho).</summary>
    private string? Resolve(string storedPath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, storedPath));
        return full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}
