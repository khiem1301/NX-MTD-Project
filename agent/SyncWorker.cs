using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NX_MTD.Agent;

/// <summary>
/// Chu kỳ đồng bộ: lấy mốc từ API → đọc lượt quẹt mới từ RJData.mdb → gửi lô lên API → lặp.
/// Việc bỏ trùng / bỏ tháng khóa / tính lại bảng công do usp_Attendance_IngestBatch lo (Rules mục 6.4).
/// Trong lúc chờ chu kỳ, poll cursor ~10s để bắt yêu cầu đồng bộ NV từ web.
/// </summary>
public sealed class SyncWorker : BackgroundService
{
    /// <summary>Trần một lô khớp IngestPunchesDtoValidator.MaxBatchSize phía backend.</summary>
    private const int MaxBatchSize = 5000;

    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CommandPollSlice = TimeSpan.FromSeconds(10);

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

            await WaitOrHandleEmployeeSyncRequestAsync(_interval, stoppingToken);
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

        await SyncEmployeesAsync(ct);

        // Dọn hết lượt tồn đọng trong một chu kỳ (chia lô nếu vượt batchSize).
        while (!ct.IsCancellationRequested)
        {
            var punches = _reader.ReadNewPunches(lastId, batchSize);
            if (punches.Count == 0)
            {
                await _api.SendHeartbeatAsync(ct);
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

    /// <summary>Chờ hết chu kỳ nhưng mỗi 10 giây kiểm tra web có yêu cầu đồng bộ NV không.</summary>
    private async Task WaitOrHandleEmployeeSyncRequestAsync(TimeSpan interval, CancellationToken ct)
    {
        var remaining = interval;
        while (remaining > TimeSpan.Zero && !ct.IsCancellationRequested)
        {
            var delay = remaining < CommandPollSlice ? remaining : CommandPollSlice;
            await Task.Delay(delay, ct);
            remaining -= delay;

            try
            {
                var cursor = await _api.GetCursorAsync(ct);
                if (cursor?.SyncEmployeesRequested == true)
                {
                    // Thoát chờ → vòng ngoài chạy SyncOnce (NV + lượt quẹt) để cập nhật mốc sức khỏe đồng bộ.
                    _logger.LogInformation("Nhận yêu cầu đồng bộ từ web; chạy chu kỳ đầy đủ ngay.");
                    return;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không kiểm tra được yêu cầu đồng bộ; thử lại sau.");
            }
        }
    }

    private async Task SyncEmployeesAsync(CancellationToken ct)
    {
        var employees = _reader.ReadEmployees();
        if (!await _api.SendEmployeesAsync(employees, ct))
        {
            _logger.LogWarning("Gửi snapshot {Count} nhân viên RJ thất bại.", employees.Count);
            return;
        }

        _logger.LogInformation("Đã đồng bộ {Count} nhân viên từ RJ (NHANVIEN).", employees.Count);
    }

    private TimeSpan ResolveInterval() =>
        _options.PollIntervalMinutes is > 0 ? TimeSpan.FromMinutes(_options.PollIntervalMinutes.Value) : DefaultInterval;
}
