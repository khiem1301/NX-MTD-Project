using System.Text.Json;
using FluentAssertions;
using NX_MTD.API.Common;

namespace Application.Tests;

public class VnDateTimeJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new VnDateTimeJsonConverter() } };

    [Fact]
    public void Write_UnspecifiedVnTime_AppendsPlus7() =>
        JsonSerializer.Serialize(new DateTime(2026, 10, 22, 8, 30, 0, DateTimeKind.Unspecified), Options)
            .Should().Be("\"2026-10-22T08:30:00+07:00\"");

    [Fact]
    public void Write_UtcTime_AppendsZ() =>
        JsonSerializer.Serialize(new DateTime(2026, 10, 22, 1, 30, 0, DateTimeKind.Utc), Options)
            .Should().Be("\"2026-10-22T01:30:00Z\"");

    [Fact]
    public void Read_UtcString_ConvertsToVn() =>
        JsonSerializer.Deserialize<DateTime>("\"2026-10-22T01:30:00Z\"", Options)
            .Should().Be(new DateTime(2026, 10, 22, 8, 30, 0));

    [Fact]
    public void Read_NoOffset_KeepsValue() =>
        JsonSerializer.Deserialize<DateTime>("\"2026-10-22T08:30:00\"", Options)
            .Should().Be(new DateTime(2026, 10, 22, 8, 30, 0));
}
