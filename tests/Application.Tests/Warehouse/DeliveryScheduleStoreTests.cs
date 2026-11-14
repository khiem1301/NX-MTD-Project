using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-113: số lần giao = số ngày giao lưu ở DeliveryScheduleDate (một chủ lịch có khoá ngoại mỗi dòng).</summary>
public class DeliveryScheduleStoreTests
{
    private static readonly DateOnly D = new(2026, 12, 1);

    private readonly List<DeliveryScheduleDate> _rows = new();
    private readonly DeliveryScheduleStore _sut;

    public DeliveryScheduleStoreTests()
    {
        var uow = Substitute.For<IUnitOfWork>();
        var repo = Substitute.For<IGenericRepository<DeliveryScheduleDate>>();
        repo.Query().Returns(_ => _rows.AsTestAsyncQueryable());
        repo.AddAsync(Arg.Any<DeliveryScheduleDate>()).Returns(call => { _rows.Add(call.Arg<DeliveryScheduleDate>()); return Task.CompletedTask; });
        repo.When(r => r.Remove(Arg.Any<DeliveryScheduleDate>())).Do(call => _rows.Remove(call.Arg<DeliveryScheduleDate>()));
        uow.Repository<DeliveryScheduleDate>().Returns(repo);
        _sut = new DeliveryScheduleStore(uow);
    }

    [Fact]
    public void Summary_OneDate_HasNoLastDate() =>
        DeliveryScheduleStore.Summary([D]).Should().Be((D, (DateOnly?)null));

    [Fact]
    public void Summary_ManyDates_IsFirstAndLast() =>
        DeliveryScheduleStore.Summary([D, D.AddDays(3), D.AddDays(9)]).Should().Be((D, (DateOnly?)D.AddDays(9)));

    [Fact]
    public async Task ReplaceAsync_StoresOneRowPerDeliveryInOrder()
    {
        await _sut.ReplaceAsync(DeliveryOwner.PurchaseOrder, 5, [D, D.AddDays(2), D.AddDays(4)], default);

        (await _sut.GetAsync(DeliveryOwner.PurchaseOrder, 5, default)).Should().Equal(D, D.AddDays(2), D.AddDays(4));
    }

    [Fact]
    public async Task ReplaceAsync_FewerDates_RemovesTheExtraDeliveries()
    {
        await _sut.ReplaceAsync(DeliveryOwner.PurchaseOrder, 5, [D, D.AddDays(2), D.AddDays(4)], default);

        await _sut.ReplaceAsync(DeliveryOwner.PurchaseOrder, 5, [D.AddDays(1)], default);

        (await _sut.GetAsync(DeliveryOwner.PurchaseOrder, 5, default)).Should().Equal(D.AddDays(1));
    }

    [Fact]
    public async Task ReplaceAsync_OtherOwnerIsUntouched()
    {
        await _sut.ReplaceAsync(DeliveryOwner.PurchaseOrder, 5, [D], default);
        await _sut.ReplaceAsync(DeliveryOwner.CustomerOrder, 5, [D.AddDays(7)], default);

        await _sut.ReplaceAsync(DeliveryOwner.PurchaseOrder, 5, [], default);

        (await _sut.GetAsync(DeliveryOwner.CustomerOrder, 5, default)).Should().Equal(D.AddDays(7));
    }

    [Fact]
    public async Task GetManyAsync_GroupsByOwner()
    {
        await _sut.ReplaceAsync(DeliveryOwner.PurchaseOrder, 1, [D, D.AddDays(1)], default);
        await _sut.ReplaceAsync(DeliveryOwner.PurchaseOrder, 2, [D.AddDays(5)], default);

        var map = await _sut.GetManyAsync(DeliveryOwner.PurchaseOrder, [1, 2, 3], default);

        map[1].Should().Equal(D, D.AddDays(1));
        map[2].Should().Equal(D.AddDays(5));
        map.Should().NotContainKey(3);
    }
}
