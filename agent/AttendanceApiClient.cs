using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NX_MTD.Agent;

public sealed record CursorResult(
    int? LastSourceRecordId, DateTime? LastSucceededAt, int SyncIntervalMinutes, bool SyncEmployeesRequested);

public sealed record IngestResult(int SyncSessionId, int Received, int Inserted, int SkippedLockedMonth, int Duplicates, int Unmapped);

/// <summary>Gọi API backend của luồng đồng bộ (UC61): mốc, lượt quẹt, snapshot NV.</summary>
public sealed class AttendanceApiClient
{
    public const string ApiKeyHeader = "X-Agent-Key";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _http;
    private readonly AgentOptions _options;

    public AttendanceApiClient(HttpClient http, IOptions<AgentOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    /// <summary>Mốc CheckInOut.ID lớn nhất đã đồng bộ và chu kỳ chạy (ATTENDANCE_SYNC_MINUTES).</summary>
    public async Task<CursorResult?> GetCursorAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/agent/attendance-sync/cursor");
        request.Headers.Add(ApiKeyHeader, _options.ApiKey);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CursorResult>>(Json, ct);
        return envelope is { Success: true } ? envelope.Data : null;
    }

    /// <summary>Gửi một lô lượt quẹt. GBR-16 do thủ tục DB lo nên gửi lại cùng lô không sinh trùng.</summary>
    public async Task<IngestResult?> SendPunchesAsync(IReadOnlyList<RjPunch> punches, CancellationToken ct)
    {
        var payload = new IngestPayload(
            punches.Select(p => new PunchPayload(p.SourceRecordId, p.AttendanceCode, p.PunchTime)).ToList(), null);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/agent/attendance-sync/punches");
        request.Headers.Add(ApiKeyHeader, _options.ApiKey);
        request.Content = JsonContent.Create(payload, options: Json);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<IngestResult>>(Json, ct);
        return envelope is { Success: true } ? envelope.Data : null;
    }

    /// <summary>Đẩy snapshot NHANVIEN (replace-all phía backend).</summary>
    public async Task<bool> SendEmployeesAsync(IReadOnlyList<RjEmployee> employees, CancellationToken ct)
    {
        var payload = new EmployeesPayload(
            employees.Select(e => new EmployeePayload(e.AttendanceCode, e.FullName, e.RjEmployeeCode)).ToList());

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/agent/attendance-sync/employees");
        request.Headers.Add(ApiKeyHeader, _options.ApiKey);
        request.Content = JsonContent.Create(payload, options: Json);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(Json, ct);
        return envelope is { Success: true };
    }

    /// <summary>Báo đã chạy chu kỳ xong khi không có lượt quẹt mới (cập nhật banner sức khỏe).</summary>
    public async Task SendHeartbeatAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/agent/attendance-sync/heartbeat");
        request.Headers.Add(ApiKeyHeader, _options.ApiKey);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private sealed record PunchPayload(int? SourceRecordId, int AttendanceCode, DateTime PunchTime);
    private sealed record IngestPayload(List<PunchPayload> Punches, string? FileName);
    private sealed record EmployeePayload(int AttendanceCode, string FullName, string? RjEmployeeCode);
    private sealed record EmployeesPayload(List<EmployeePayload> Employees);
    private sealed record ApiEnvelope<T>(bool Success, T? Data, string? Message, string? ErrorCode);
}
