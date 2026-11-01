using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NX_MTD.Infrastructure.Storage;

namespace Application.Tests;

/// <summary>LocalFileStorage trên một thư mục tạm riêng của từng lần chạy test.</summary>
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nxmtd-storage-" + Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        var config = Substitute.For<IConfiguration>();
        config["Storage:LocalRoot"].Returns(_root);
        _storage = new LocalFileStorage(config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task List_returns_every_saved_file_of_the_folder_with_slash_keys_and_recent_write_time()
    {
        var a = await _storage.SaveAsync([1], "PRODUCT", ".jpg", default);
        var b = await _storage.SaveAsync([2], "PRODUCT", ".png", default);
        await _storage.SaveAsync([3], "REQUEST", ".jpg", default);

        var files = await _storage.ListAsync("PRODUCT", default);

        files.Select(f => f.Key).Should().BeEquivalentTo(a, b);
        files.Should().OnlyContain(f => !f.Key.Contains('\\') && DateTime.UtcNow - f.LastWriteUtc < TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task List_of_a_folder_that_does_not_exist_is_empty() =>
        (await _storage.ListAsync("PRODUCT", default)).Should().BeEmpty();

    [Fact]
    public async Task Delete_removes_the_file_and_reports_whether_it_existed()
    {
        var key = await _storage.SaveAsync([1], "PRODUCT", ".jpg", default);

        (await _storage.DeleteAsync(key, default)).Should().BeTrue();
        (await _storage.OpenReadAsync(key, default)).Should().BeNull();
        (await _storage.DeleteAsync(key, default)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_refuses_a_key_that_points_outside_the_storage_root()
    {
        var outside = Path.Combine(Path.GetTempPath(), "nxmtd-outside-" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(outside, "x");
        try
        {
            var key = "../" + Path.GetFileName(outside);

            (await _storage.DeleteAsync(key, default)).Should().BeFalse();
            File.Exists(outside).Should().BeTrue();
        }
        finally
        {
            File.Delete(outside);
        }
    }
}
