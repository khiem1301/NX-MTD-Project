using FluentAssertions;
using NX_MTD.Application.Common;

namespace Application.Tests;

public class ShortfallNoticeTests
{
    [Fact]
    public void Build_OnlyLinesReceivedLess_ListsSentReceivedAndReason()
    {
        var text = ShortfallNotice.Build("Phiếu XH-1", [
            new ShortfallLine("AO01 Đỏ-M", 10, 8, "Thiếu 2"),
            new ShortfallLine("AO01 Đỏ-L", 5, 5, null),
        ]);

        text.Should().Be("Phiếu XH-1: AO01 Đỏ-M: gửi 10, nhận 8 (lý do: Thiếu 2).");
    }

    [Fact]
    public void Build_NoLineReceivedLess_ReturnsNull()
    {
        ShortfallNotice.Build("Phiếu XH-1", [new ShortfallLine("AO01 Đỏ-M", 10, 10, null)]).Should().BeNull();
    }

    [Fact]
    public void Build_VeryLongText_IsCutToNotificationColumnLimit()
    {
        var lines = Enumerable.Range(0, 60).Select(i => new ShortfallLine($"Dòng {i}", 10, 1, new string('x', 40)));

        ShortfallNotice.Build("Phiếu", lines)!.Length.Should().Be(1000);
    }
}
