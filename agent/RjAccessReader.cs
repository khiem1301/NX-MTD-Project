using System.Data.OleDb;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace NX_MTD.Agent;

/// <summary>Một lượt quẹt đọc từ bảng CheckInOut của Ronald Jack Pro.</summary>
public sealed record RjPunch(int SourceRecordId, int AttendanceCode, DateTime PunchTime);

/// <summary>Một nhân viên từ bảng NHANVIEN (chỉ mã + tên).</summary>
public sealed record RjEmployee(int AttendanceCode, string FullName, string? RjEmployeeCode);

/// <summary>
/// Đọc RJData.mdb (Access) bằng OleDb.
/// CheckInOut: ID, MaChamCong, GioCham. NHANVIEN: MaChamCong, TenNhanVien, MaNhanVien.
/// Không đọc vân tay, mật khẩu, ảnh.
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

    /// <summary>Đọc nhân viên đang bật trên RJ (UserEnable = True).</summary>
    public IReadOnlyList<RjEmployee> ReadEmployees()
    {
        var rows = new List<RjEmployee>();
        var connectionString = $"Provider={_options.Provider};Data Source={_options.DbPath};";

        using var connection = new OleDbConnection(connectionString);
        connection.Open();

        // Access: UserEnable là Yes/No; MaChamCong có thể Number.
        const string sql =
            "SELECT MaChamCong, TenNhanVien, MaNhanVien FROM NHANVIEN WHERE UserEnable = True";
        using var command = new OleDbCommand(sql, connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!TryToInt(reader.GetValue(0), out var code) || code <= 0) continue;
            var name = Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture)?.Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var rjCode = Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture)?.Trim();
            rows.Add(new RjEmployee(code, name, string.IsNullOrWhiteSpace(rjCode) ? null : rjCode));
        }

        return rows;
    }

    private static bool TryToInt(object value, out int result)
    {
        try
        {
            result = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }

    /// <summary>GioCham thường là Date/Time; đề phòng file xuất giữ giá trị thành chuỗi.</summary>
    private static DateTime ParsePunchTime(object value) => value switch
    {
        DateTime dateTime => dateTime,
        string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
        _ => Convert.ToDateTime(value, CultureInfo.InvariantCulture),
    };
}
