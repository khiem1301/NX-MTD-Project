using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

public class StockServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);
    private static readonly StockKey Fabric = StockKey.Material(1, "Đỏ");
    private static readonly StockKey Piece = StockKey.SemiFinished(9);

    private readonly IStockRepository _repo = Substitute.For<IStockRepository>();
    private readonly IAppLock _locks = Substitute.For<IAppLock>();
    private readonly StockService _sut;

    public StockServiceTests()
    {
        _sut = new StockService(_repo, _locks);
        _repo.DescribeAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns("NVL");
    }

    private static StockMovement Move(StockKey key, decimal qty, int lineId = 1) =>
        new(key, qty, StockDocTypes.MaterialIssue, 100, lineId, Today);

    [Fact]
    public async Task PostAsync_BalanceWouldGoNegative_ReturnsFailureAndWritesNothing()
    {
        _repo.GetOnHandAsync(Fabric, Arg.Any<CancellationToken>()).Returns(5m);

        var result = await _sut.PostAsync(new[] { Move(Fabric, -8m) }, default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInsufficientStock);
        await _repo.DidNotReceive().AddAsync(Arg.Any<StockTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostAsync_OneLineFailsAmongMany_WritesNothing()
    {
        _repo.GetOnHandAsync(Fabric, Arg.Any<CancellationToken>()).Returns(100m);
        _repo.GetOnHandAsync(Piece, Arg.Any<CancellationToken>()).Returns(0m);

        var result = await _sut.PostAsync(new[] { Move(Fabric, -10m), Move(Piece, -1m, 2) }, default);

        result.IsSuccess.Should().BeFalse();
        await _repo.DidNotReceive().AddAsync(Arg.Any<StockTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostAsync_SameKeyTwice_AccumulatesBalanceAfter()
    {
        _repo.GetOnHandAsync(Fabric, Arg.Any<CancellationToken>()).Returns(10m);
        var written = new List<StockTransaction>();
        await _repo.AddAsync(Arg.Do<StockTransaction>(written.Add), Arg.Any<CancellationToken>());

        var result = await _sut.PostAsync(new[] { Move(Fabric, 5m, 1), Move(Fabric, -12m, 2) }, default);

        result.IsSuccess.Should().BeTrue();
        written.Select(t => t.BalanceAfter).Should().Equal(15m, 3m);
        written.Should().AllSatisfy(t =>
        {
            t.WarehouseType.Should().Be(WarehouseTypes.Material);
            t.MaterialId.Should().Be(1);
            t.MaterialColor.Should().Be("Đỏ");
            t.OrderLineId.Should().BeNull();
        });
        await _repo.Received(1).GetOnHandAsync(Fabric, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostAsync_ExactlyEmptiesStock_Succeeds()
    {
        _repo.GetOnHandAsync(Piece, Arg.Any<CancellationToken>()).Returns(7m);

        var result = await _sut.PostAsync(new[] { Move(Piece, -7m) }, default);

        result.IsSuccess.Should().BeTrue();
        await _repo.Received(1).AddAsync(Arg.Is<StockTransaction>(t => t.BalanceAfter == 0m && t.OrderLineId == 9),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostAsync_ZeroQuantity_Throws()
    {
        var act = () => _sut.PostAsync(new[] { Move(Fabric, 0m) }, default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PostAsync_ManyKeys_LocksEachKeyOnceInFixedOrder()
    {
        _repo.GetOnHandAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(100m);

        await _sut.PostAsync(new[] { Move(Piece, 1m), Move(Fabric, 1m, 2), Move(Piece, 1m, 3) }, default);

        var expected = new[] { Fabric.LockName, Piece.LockName }.OrderBy(n => n, StringComparer.Ordinal).ToList();
        Received.InOrder(() =>
        {
            _locks.AcquireAsync(expected[0], Arg.Any<CancellationToken>());
            _locks.AcquireAsync(expected[1], Arg.Any<CancellationToken>());
        });
        await _locks.Received(2).AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureAvailableAsync_DuplicateKeysExceedAvailable_ReturnsFailure()
    {
        _repo.GetAvailableAsync(Fabric, Arg.Any<CancellationToken>()).Returns(10m);

        var result = await _sut.EnsureAvailableAsync(
            new[] { new StockRequirement(Fabric, 6m), new StockRequirement(Fabric, 5m) }, default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInsufficientStock);
    }

    [Fact]
    public async Task EnsureAvailableAsync_EnoughAvailable_SucceedsAfterLocking()
    {
        _repo.GetAvailableAsync(Fabric, Arg.Any<CancellationToken>()).Returns(10m);

        var result = await _sut.EnsureAvailableAsync(new[] { new StockRequirement(Fabric, 10m) }, default);

        result.IsSuccess.Should().BeTrue();
        await _locks.Received(1).AcquireAsync(Fabric.LockName, Arg.Any<CancellationToken>());
    }
}
