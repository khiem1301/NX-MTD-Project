using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Storage;

/// <summary>
/// DEV-ALL-148: lưu tạm bản gợi ý AI của mỗi tổ thành một file JSON trong thư mục cục bộ (mặc định
/// <c>App_Data/grade-suggestions</c>, đổi bằng <c>GradeSuggestion:RootPath</c>). Không thêm bảng DB vì đây là bản nháp ngắn hạn;
/// hết hạn thì service xóa. Thư mục không được phục vụ như file tĩnh.
/// </summary>
public sealed class LocalGradeSuggestionStore : IGradeSuggestionStore
{
    // Một khóa cho cả kho: tổ trưởng và tổ phó cùng sửa thì ghi lần lượt, không làm hỏng file.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _root;

    public LocalGradeSuggestionStore(IConfiguration config, IHostEnvironment env)
    {
        var configured = config["GradeSuggestion:RootPath"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "App_Data", "grade-suggestions")
            : configured);
    }

    private string PathOf(int teamId) => Path.Combine(_root, $"team-{teamId}.json");

    public async Task<GradeSuggestionDto?> GetAsync(int teamId, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var path = PathOf(teamId);
            if (!File.Exists(path)) return null;
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<GradeSuggestionDto>(stream, Json, ct);
        }
        finally { Gate.Release(); }
    }

    public async Task SaveAsync(GradeSuggestionDto draft, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(_root);
            var path = PathOf(draft.TeamId);
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(draft, Json), ct);
            File.Move(temp, path, overwrite: true);
        }
        finally { Gate.Release(); }
    }

    public async Task DeleteAsync(int teamId, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var path = PathOf(teamId);
            if (File.Exists(path)) File.Delete(path);
        }
        finally { Gate.Release(); }
    }
}
