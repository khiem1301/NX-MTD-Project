using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;

namespace Application.Tests;

/// <summary>Cơ điện xin linh kiện, vật tư máy, máy mới: số nguyên, có trần.</summary>
public class MachineRequestHandlerTests
{
    private static readonly DateOnly Day = new(2026, 11, 7);
    private readonly IClock _clock = Substitute.For<IClock>();

    public MachineRequestHandlerTests() => _clock.Today.Returns(Day);

    private static RequestLineInput Line(decimal qty, string name = "Kim máy") => new(null, null, null, name, qty);

    private static RequestDraft Draft(string type, params RequestLineInput[] lines) =>
        new(type, null, null, null, null, null, null, null, "Xin", "Nội dung", lines, null);

    private Task<NX_MTD.Application.Common.Result> Check(MachineRequestHandler handler, string type, params RequestLineInput[] lines) =>
        handler.ValidateAsync(Draft(type, lines), default);

    [Theory]
    [InlineData(1, true)]
    [InlineData(9_999, true)]
    [InlineData(10_000, false)]
    [InlineData(0, false)]
    [InlineData(2.5, false)]
    [InlineData(0.001, false)]
    public async Task SpareParts_AreWholeNumbersUpToNineThousandNineHundredNinetyNine(double qty, bool valid)
    {
        var result = await Check(new SparePartsRequestHandler(_clock), RequestTypes.SpareParts, Line((decimal)qty));

        result.IsSuccess.Should().Be(valid);
        if (!valid) result.Message.Should().Contain("số nguyên");
    }

    [Theory]
    [InlineData(99, true)]
    [InlineData(100, false)]
    [InlineData(1.5, false)]
    public async Task NewMachine_AreWholeNumbersUpToNinetyNine(double qty, bool valid) =>
        (await Check(new NewMachineRequestHandler(_clock), RequestTypes.NewMachine, Line((decimal)qty))).IsSuccess.Should().Be(valid);

    [Fact]
    public async Task MachineSupplies_UseThePartsLimit() =>
        (await Check(new MachineSuppliesRequestHandler(_clock), RequestTypes.MachineSupplies, Line(9_999))).IsSuccess.Should().BeTrue();

    [Fact]
    public async Task Requests_NeedALine_AndRejectDuplicateItems()
    {
        var handler = new SparePartsRequestHandler(_clock);

        (await Check(handler, RequestTypes.SpareParts)).IsSuccess.Should().BeFalse();
        (await Check(handler, RequestTypes.SpareParts, Line(1, "Kim máy"), Line(2, " kim MÁY "))).IsSuccess.Should().BeFalse();
    }
}
