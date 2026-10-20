using System.Data.OleDb;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace NX_MTD.Agent;

/// <summary>Một lượt quẹt đọc từ bảng CheckInOut của Ronald Jack Pro.</summary>
public sealed record RjPunch(int SourceRecordId, int AttendanceCode, DateTime PunchTime);

/// <summary>
/// Đọc RJData.mdb (Access) bằng OleDb, chỉ lấy 3 cột theo Rules mục 4.6:
/// ID → SourceRecordId, MaChamCong → AttendanceCode, GioCham → PunchTime.
/// Không đọc vân tay, mật khẩu, ảnh hay bảng nhân viên/máy của RJ.
/// </summary>
public sealed class RjAccessReader
{
    private readonly AgentOptions _options;

    public RjAccessReader(IOptions<AgentOptions> options) => _options = options.Value;

    /// <summary>Đọc tối đa <paramref name="batchSize"/> lượt quẹt có ID lớn hơn mốc, theo thứ tự tăng dần.</summary>
    public IReadOnlyList<RjPunch> ReadNewPunches(int afterSourceRecordId, int batchSize)
    {
        var punches = new List<RjPunch>(batchSize);
        var connectionString = $"Provider={_options.Provider};Data Source={_options.DbPath};";

        using var connection = new OleDbConnection(connectionString);
        connection.Open();

        const string sql = "SELECT ID, MaChamCong, GioCham FROM CheckInOut WHERE ID > ? ORDER BY ID";
        using var command = new OleDbCommand(sql, connection);
        command.Parameters.Add("?", OleDbType.Integer).Value = afterSourceRecordId;

        using var reader = command.ExecuteReader();
        while (reader.Read() && punches.Count < batchSize)
        {
            punches.Add(new RjPunch(
                Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                ParsePunchTime(reader.GetValue(2))));
        }

        return punches;
    }

    /// <summary>GioCham thường là Date/Time; đề phòng file xuất giữ giá trị thành chuỗi.</summary>
    private static DateTime ParsePunchTime(object value) => value switch
    {
        DateTime dateTime => dateTime,
        string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
        _ => Convert.ToDateTime(value, CultureInfo.InvariantCulture),
    };
}
