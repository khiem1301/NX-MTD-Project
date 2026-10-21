using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Audit;

public sealed class AuditLogService : IAuditLogService
{
    // Giữ tiếng Việt dễ đọc trong NVARCHAR, tên field PascalCase như cột DB.
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly NxMtdDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public AuditLogService(NxMtdDbContext db, ICurrentUser user, IClock clock) { _db = db; _user = user; _clock = clock; }

    public void Add(AuditEntry entry) =>
        _db.AuditLogs.Add(new AuditLog
        {
            TableName = entry.TableName,
            RecordId = entry.RecordId,
            Action = entry.Action,
            OldValues = entry.OldValues is null ? null : JsonSerializer.Serialize(entry.OldValues, Json),
            NewValues = entry.NewValues is null ? null : JsonSerializer.Serialize(entry.NewValues, Json),
            Reason = entry.Reason,
            ChangedBy = _user.IsAuthenticated ? _user.EmployeeId : null,
            ChangedAt = _clock.Now,
        });

    public async Task<IReadOnlyList<AuditLogDto>> GetHistoryAsync(string tableName, long recordId, CancellationToken ct) =>
        await _db.AuditLogs.AsNoTracking()
            .Where(a => a.TableName == tableName && a.RecordId == recordId)
            .OrderBy(a => a.ChangedAt).ThenBy(a => a.AuditLogId)
            .Select(a => new AuditLogDto(a.AuditLogId, a.Action, a.OldValues, a.NewValues, a.Reason, a.ChangedBy,
                a.ChangedByNavigation == null ? null : a.ChangedByNavigation.FullName, a.ChangedAt))
            .ToListAsync(ct);
}
