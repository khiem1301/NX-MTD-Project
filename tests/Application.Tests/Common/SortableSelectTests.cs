using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace Application.Tests;

/// <summary>Sắp xếp phía máy chủ (DEV-ALL-197): kiểm câu SQL EF sinh ra, không cần kết nối DB.</summary>
public class SortableSelectTests
{
    private static NxMtdDbContext NewContext() => new(new DbContextOptionsBuilder<NxMtdDbContext>()
        .UseSqlServer("Server=.;Database=sort_probe;Trusted_Connection=True;TrustServerCertificate=True").Options);

    private static string Sql(string? sortBy, bool desc)
    {
        using var ctx = NewContext();
        var query = ctx.FgHandovers.OrderByDescending(h => h.FgHandoverId);
        var sorted = SortableSelect.OrderByRequested(query, (System.Linq.Expressions.Expression<Func<FgHandover, FgHandoverListItemDto>>)(h =>
            new FgHandoverListItemDto(h.FgHandoverId, h.HandoverCode, h.OrderId, h.Order.OrderCode, h.ProductId, h.Product.ProductCode,
                h.InspectorId, h.Inspector.FullName, h.HandedAt, h.Status, null, h.ConfirmedAt, h.FgHandoverLines.Sum(l => l.HandedQty))), sortBy, desc);
        return sorted.Select(h => h.FgHandoverId).ToQueryString();
    }

    [Fact]
    public void OrderByRequested_ColumnOfRelatedEntity_SortsByThatColumnThenById()
    {
        var sql = Sql("orderCode", desc: false);

        sql.Should().Contain("ORDER BY [c].[OrderCode], [f].[FgHandoverId]");
    }

    [Fact]
    public void OrderByRequested_AggregateColumn_IsTranslatedToSql()
    {
        Sql("totalHandedQty", desc: true).Should().Contain("SUM(").And.Contain("DESC");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("khongCoCot")]
    public void OrderByRequested_NoOrUnknownColumn_KeepsDefaultOrder(string? sortBy)
    {
        var sql = Sql(sortBy, desc: false);

        sql.Should().Contain("ORDER BY [f].[FgHandoverId] DESC");
    }

    [Theory]
    [InlineData("materialCode", "ORDER BY [m].[MaterialCode]")]
    [InlineData("availableQty", "ORDER BY COALESCE([v].[AvailableQty], 0.0)")]
    public void OrderByRequested_JoinedQueryProjectedThroughAnonymousType_IsTranslatedToSql(string sortBy, string expected)
    {
        using var ctx = NewContext();
        var q = from b in ctx.VwStockBalances.AsNoTracking()
                join m in ctx.Materials on b.MaterialId equals m.MaterialId
                orderby m.MaterialCode
                select new { b, m };
        var sorted = SortableSelect.OrderByRequested(q, x => new StockBalanceDto(x.b.WarehouseType, x.m.MaterialId, x.m.MaterialCode,
            x.m.MaterialName, x.m.MaterialType, x.m.Unit, x.m.Color, null, null, null, null, null, null,
            x.b.OnHandQty ?? 0, x.b.ReservedQty ?? 0, x.b.AvailableQty ?? 0, null, null), sortBy, desc: false);

        sorted.Select(x => x.m.MaterialId).ToQueryString().Should().Contain(expected);
    }

    private sealed record Row(string Name, decimal Qty, int? Opt);

    [Fact]
    public void OrderInMemory_SortsByPropertyIgnoringCase_EmptyValuesFirstAndUnknownColumnKeepsOrder()
    {
        Row[] rows = [new("Bình", 3m, 2), new("An", 5m, null), new("Chi", 1m, 1)];

        SortableSelect.OrderInMemory(rows, "NAME", desc: false).Select(r => r.Name).Should().Equal("An", "Bình", "Chi");
        SortableSelect.OrderInMemory(rows, "qty", desc: true).Select(r => r.Name).Should().Equal("An", "Bình", "Chi");
        SortableSelect.OrderInMemory(rows, "opt", desc: false).Select(r => r.Name).Should().Equal("An", "Chi", "Bình");
        SortableSelect.OrderInMemory(rows, "khongCo", desc: false).Should().Equal(rows);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("desc", true)]
    [InlineData("ASC", false)]
    public void IsDescending_DefaultsToDescendingUnlessAsc(string? sortDir, bool expected) =>
        SortableSelect.IsDescending(sortDir).Should().Be(expected);
}
