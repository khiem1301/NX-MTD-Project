using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NX_MTD.API.Common;

/// <summary>D21: DateTime giờ VN ↔ "yyyy-MM-ddTHH:mm:ss+07:00"; DateTime UTC (hạn token) ghi đuôi Z.</summary>
public sealed partial class VnDateTimeJsonConverter : JsonConverter<DateTime>
{
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex OffsetSuffix();

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString() ?? throw new JsonException("Ngày giờ trống.");
        if (!OffsetSuffix().IsMatch(text))
            return DateTime.SpecifyKind(DateTime.Parse(text, CultureInfo.InvariantCulture), DateTimeKind.Unspecified);
        var withOffset = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
        return DateTime.SpecifyKind(withOffset.ToOffset(VnOffset).DateTime, DateTimeKind.Unspecified);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            return;
        }
        var vn = new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), VnOffset);
        // WriteRawValue: encoder mặc định sẽ escape dấu "+" thành mã unicode. Chuỗi này do mình dựng nên an toàn.
        writer.WriteRawValue($"\"{vn.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)}\"");
    }
}
