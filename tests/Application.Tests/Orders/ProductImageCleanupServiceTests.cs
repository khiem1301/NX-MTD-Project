using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Dọn ảnh mồ côi của mã hàng: chỉ xóa file cũ, đúng dạng khóa ảnh mã hàng và không còn dòng nào trỏ tới.</summary>
public class ProductImageCleanupServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 2, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Old = Now.AddDays(-3);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();
    private readonly List<Product> _products = [];
    private readonly List<ProductImage> _images = [];
    private readonly List<ProductColorImage> _colorImages = [];
    private readonly List<StoredFile> _files = [];

    public ProductImageCleanupServiceTests()
    {
        Setup(_products);
        Setup(_images);
        Setup(_colorImages);
        _storage.ListAsync("PRODUCT", Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult<IReadOnlyList<StoredFile>>(_files.ToList()));
        _storage.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private static string Key(char c, string ext = "jpg") => $"PRODUCT/2026/11/{new string(c, 32)}.{ext}";

    private ProductImageCleanupService Create() => new(_uow, _storage, NullLogger<ProductImageCleanupService>.Instance);

    private string[] Deleted() => _storage.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IFileStorage.DeleteAsync))
        .Select(c => (string)c.GetArguments()[0]!).ToArray();

    [Fact]
    public async Task Deletes_only_old_files_that_no_product_uses()
    {
        _products.Add(new Product { ProductId = 1, SampleImageUrl = Key('a') });
        _images.Add(new ProductImage { ProductId = 1, ImageKey = Key('b', "png") });
        _files.AddRange([new(Key('a'), Old), new(Key('b', "png"), Old), new(Key('c'), Old), new(Key('d', "webp"), Old)]);

        var deleted = await Create().RunAsync(Now, default);

        deleted.Should().Be(2);
        Deleted().Should().BeEquivalentTo(Key('c'), Key('d', "webp"));
    }

    [Fact]
    public async Task Keeps_files_used_as_color_images()
    {
        _colorImages.Add(new ProductColorImage { ProductColorId = 1, ImageKey = Key('a'), SortOrder = 1 });
        _files.AddRange([new(Key('a'), Old), new(Key('b'), Old)]);

        (await Create().RunAsync(Now, default)).Should().Be(1);

        Deleted().Should().BeEquivalentTo(Key('b'));
    }

    [Fact]
    public async Task Keeps_recent_files_even_when_nothing_uses_them_because_the_form_may_still_be_open()
    {
        _files.AddRange([new(Key('a'), Now.AddHours(-1)), new(Key('b'), Now - ProductImageCleanupService.GracePeriod)]);

        (await Create().RunAsync(Now, default)).Should().Be(0);

        Deleted().Should().BeEmpty();
    }

    [Fact]
    public async Task Deletes_a_file_just_past_the_grace_period()
    {
        _files.Add(new(Key('a'), Now - ProductImageCleanupService.GracePeriod - TimeSpan.FromSeconds(1)));

        (await Create().RunAsync(Now, default)).Should().Be(1);
    }

    [Theory]
    [InlineData("PRODUCT/readme.txt")]
    [InlineData("PRODUCT/2026/11/short.jpg")]
    [InlineData("PRODUCT/2026/11/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.gif")]
    [InlineData("PRODUCT/../REQUEST/2026/11/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg")]
    public async Task Never_touches_files_that_are_not_product_image_keys(string key)
    {
        _files.Add(new(key, Old));

        (await Create().RunAsync(Now, default)).Should().Be(0);

        Deleted().Should().BeEmpty();
    }

    [Fact]
    public async Task No_old_files_means_no_database_queries()
    {
        _files.Add(new(Key('a'), Now.AddMinutes(-5)));

        await Create().RunAsync(Now, default);

        _uow.DidNotReceiveWithAnyArgs().Repository<Product>();
    }

    [Fact]
    public async Task A_file_that_cannot_be_deleted_is_skipped_and_the_rest_are_still_cleaned()
    {
        _files.AddRange([new(Key('a'), Old), new(Key('b'), Old), new(Key('c'), Old)]);
        _storage.DeleteAsync(Key('a'), Arg.Any<CancellationToken>()).Returns<Task<bool>>(_ => throw new IOException("đang bị khóa"));

        var deleted = await Create().RunAsync(Now, default);

        deleted.Should().Be(2);
        Deleted().Should().BeEquivalentTo(Key('a'), Key('b'), Key('c'));
    }

    [Fact]
    public async Task A_file_already_gone_is_not_counted()
    {
        _files.Add(new(Key('a'), Old));
        _storage.DeleteAsync(Key('a'), Arg.Any<CancellationToken>()).Returns(false);

        (await Create().RunAsync(Now, default)).Should().Be(0);
    }
}
