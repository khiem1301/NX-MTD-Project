using Microsoft.Extensions.Configuration;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Storage;

/// <summary>Lưu ảnh trên ổ đĩa máy chạy API (dev, demo). S3: KH-25 (D19).</summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration config)
    {
        var configured = config["Storage:LocalRoot"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads")
            : configured);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(byte[] content, string folder, string extension, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var key = $"{folder}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}{extension}";
        var path = Resolve(key) ?? throw new InvalidOperationException("Invalid storage key");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, ct);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        Stream? stream = path is not null && File.Exists(path) ? File.OpenRead(path) : null;
        return Task.FromResult(stream);
    }

    /// <summary>Chặn "../" ra ngoài thư mục gốc.</summary>
    private string? Resolve(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        return full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
    }
}
