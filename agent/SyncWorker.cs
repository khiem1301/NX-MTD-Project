using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NX_MTD.Agent;

/// <summary>
/// Chu kỳ đồng bộ: lấy mốc từ API → đọc lượt quẹt mới từ RJData.mdb → gửi lô lên API → lặp.
/// Việc bỏ trùng / bỏ tháng khóa / tính lại bảng công do usp_Attendance_IngestBatch lo (Rules mục 6.4).
/// </summary>
public sealed class SyncWorker : BackgroundService
{
    /// <summary>Trần một lô khớp IngestPunchesDtoValidator.MaxBatchSize phía backend.</summary>
    private const int MaxBatchSize = 5000;

    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(10);

    private readonly AttendanceApiClient _api;
    private readonly RjAccessReader _reader;
    private readonly AgentOptions _options;
    private readonly ILogger<SyncWorker> _logger;

    private TimeSpan _interval = DefaultInterval;

    public SyncWorker(AttendanceApiClient api, RjAccessReader reader, IOptions<AgentOptions> options,
        ILogger<SyncWorker> logger)
    {
        _api = api;
        _reader = reader;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!ValidateConfiguration()) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _interval = await SyncOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi chu kỳ đồng bộ chấm công. Thử lại sau {Interval} phút.", _interval.TotalMinutes);
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private bool ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogError("Chưa cấu hình Agent:ApiKey. Đặt khóa giống Attendance:AgentApiKey phía backend rồi chạy lại.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.DbPath))
        {
            _logger.LogError("Chưa cấu hình Agent:DbPath (đường dẫn tới RJData.mdb).");
            return false;
        }

        return true;
    }

    private async Task<TimeSpan> SyncOnceAsync(CancellationToken ct)
    {
        var cursor = await _api.GetCursorAsync(ct);
        var lastId = cursor?.LastSourceRecordId ?? 0;
        var batchSize = Math.Clamp(_options.BatchSize, 1, MaxBatchSize);

        var interval = cursor is { SyncIntervalMinutes: > 0 }
            ? TimeSpan.FromMinutes(cursor.SyncIntervalMinutes)
            : ResolveInterval();

        // Dọn hết lượt tồn đọng trong một chu kỳ (chia lô nếu vượt batchSize).
        while (!ct.IsCancellationRequested)
        {
            var punches = _reader.ReadNewPunches(lastId, batchSize);
            if (punches.Count == 0)
            {
                _logger.LogInformation("Đồng bộ xong, mốc hiện tại ID {LastId}. Chờ {Interval} phút.",
                    lastId, interval.TotalMinutes);
                break;
            }

            var result = await _api.SendPunchesAsync(punches, ct);
            if (result is null)
            {
                _logger.LogWarning("Gửi lô {Count} lượt thất bại; thử lại ở chu kỳ sau.", punches.Count);
                break;
            }

            _logger.LogInformation(
                "Đã gửi {Count} lượt quẹt: mới {Inserted}, trùng {Duplicates}, khóa tháng {Locked}, chưa map {Unmapped}.",
                punches.Count, result.Inserted, result.Duplicates, result.SkippedLockedMonth, result.Unmapped);

            lastId = punches[^1].SourceRecordId;
        }

        return interval;
    }

    private TimeSpan ResolveInterval() =>
        _options.PollIntervalMinutes is > 0 ? TimeSpan.FromMinutes(_options.PollIntervalMinutes.Value) : DefaultInterval;
}
