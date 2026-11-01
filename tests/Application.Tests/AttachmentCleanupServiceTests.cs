using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Dọn tệp chứng từ: ảnh đã gỡ quá 15 ngày và file không còn dòng Attachment nào trỏ tới quá 15 ngày.</summary>
public class AttachmentCleanupServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 2, 30, 0);
    private static readonly DateTime UtcNow = new(2026, 11, 19, 19, 30, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(AttachmentCleanupService.RetentionDays);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly List<Attachment> _attachments = [];
    private readonly List<StoredFile> _files = [];
    private long _nextId = 1;

    public AttachmentCleanupServiceTests()
    {
        _clock.Now.Returns(Now);
        var repo = Substitute.For<IGenericRepository<Attachment>>();
        repo.Query().Returns(_ => _attachments.AsTestAsyncQueryable());
        repo.When(r => r.RemoveRange(Arg.Any<IEnumerable<Attachment>>()))
            .Do(c => { foreach (var a in c.Arg<IEnumerable<Attachment>>().ToList()) _attachments.Remove(a); });
        _uow.Repository<Attachment>().Returns(repo);
        _storage.ListAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(c => Task.FromResult<IReadOnlyList<StoredFile>>(_files.Where(f => f.Key.StartsWith(c.Arg<string>() + "/")).ToList()));
        // Kho giả giống kho thật: xóa xong thì file không còn trong danh sách.
        _storage.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(c => Task.FromResult(_files.RemoveAll(x => x.Key == c.Arg<string>()) > 0));
    }

    private static string Key(char c, string folder = "REQUEST") => $"{folder}/2026/10/{new string(c, 32)}.jpg";

    private Attachment Add(string key, bool current, DateTime? removedAt = null, string owner = AttachmentOwnerTypes.Request)
    {
        var a = new Attachment { AttachmentId = _nextId++, OwnerType = owner, OwnerId = 1, FileUrl = key, IsCurrent = current, RemovedAt = removedAt };
        _attachments.Add(a);
        return a;
    }

    private AttachmentCleanupService Create() => new(_uow, _storage, _clock, NullLogger<AttachmentCleanupService>.Instance);

    private string[] Deleted() => _storage.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IFileStorage.DeleteAsync))
        .Select(c => (string)c.GetArguments()[0]!).ToArray();

    [Fact]
    public async Task Removed_image_older_than_15_days_loses_its_file_and_its_row()
    {
        var old = Add(Key('a'), current: false, removedAt: Now - Retention - TimeSpan.FromMinutes(1));
        _files.Add(new(Key('a'), UtcNow.AddDays(-30)));

        var deleted = await Create().RunAsync(UtcNow, default);

        deleted.Should().Be(1);
        Deleted().Should().Equal(Key('a'));
        _attachments.Should().NotContain(old);
    }

    [Fact]
    public async Task Removed_image_within_15_days_is_kept()
    {
        var recent = Add(Key('a'), current: false, removedAt: Now - Retention);
        _files.Add(new(Key('a'), UtcNow.AddDays(-30)));

        (await Create().RunAsync(UtcNow, default)).Should().Be(0);

        Deleted().Should().BeEmpty();
        _attachments.Should().Contain(recent);
    }

    [Fact]
    public async Task Current_images_are_never_touched_however_old()
    {
        var current = Add(Key('a'), current: true);
        _files.Add(new(Key('a'), UtcNow.AddDays(-400)));

        (await Create().RunAsync(UtcNow, default)).Should().Be(0);

        _attachments.Should().Contain(current);
    }

    [Fact]
    public async Task A_removed_image_is_kept_while_it_has_no_removal_time()
    {
        var unknown = Add(Key('a'), current: false, removedAt: null);

        (await Create().RunAsync(UtcNow, default)).Should().Be(0);

        _attachments.Should().Contain(unknown);
    }

    [Theory]
    [InlineData(AttachmentOwnerTypes.Payroll)]
    [InlineData(AttachmentOwnerTypes.AttendanceImport)]
    public async Task Payroll_and_import_files_have_their_own_store_and_are_left_alone(string owner)
    {
        var row = Add("2026-10/aaaa.xlsx", current: false, removedAt: Now.AddDays(-100), owner: owner);

        (await Create().RunAsync(UtcNow, default)).Should().Be(0);

        Deleted().Should().BeEmpty();
        _attachments.Should().Contain(row);
    }

    [Fact]
    public async Task A_file_that_cannot_be_deleted_keeps_its_row_for_the_next_run_and_the_others_are_still_cleaned()
    {
        var stuck = Add(Key('a'), current: false, removedAt: Now.AddDays(-20));
        var fine = Add(Key('b'), current: false, removedAt: Now.AddDays(-20));
        _files.AddRange([new(Key('a'), UtcNow.AddDays(-30)), new(Key('b'), UtcNow.AddDays(-30))]);
        _storage.DeleteAsync(Key('a'), Arg.Any<CancellationToken>()).Returns<Task<bool>>(_ => throw new IOException("đang bị khóa"));

        (await Create().RunAsync(UtcNow, default)).Should().Be(1);

        _attachments.Should().Contain(stuck).And.NotContain(fine);
    }

    [Fact]
    public async Task A_removed_row_whose_file_is_already_gone_is_still_removed_but_not_counted()
    {
        var row = Add(Key('a'), current: false, removedAt: Now.AddDays(-20));
        _storage.DeleteAsync(Key('a'), Arg.Any<CancellationToken>()).Returns(false);

        (await Create().RunAsync(UtcNow, default)).Should().Be(0);

        _attachments.Should().NotContain(row);
    }

    [Fact]
    public async Task Unreferenced_old_file_is_deleted_but_referenced_and_recent_ones_are_kept()
    {
        Add(Key('a'), current: true);
        Add(Key('b'), current: false, removedAt: Now.AddDays(-3));   // đã gỡ nhưng còn trong hạn giữ
        _files.AddRange([
            new(Key('a'), UtcNow.AddDays(-60)),
            new(Key('b'), UtcNow.AddDays(-60)),
            new(Key('c'), UtcNow.AddDays(-60)),                      // không dòng nào trỏ tới, đã cũ
            new(Key('d'), UtcNow.AddDays(-2)),                       // không dòng nào trỏ tới nhưng còn mới
            new(Key('e', "PURCHASE_ORDER"), UtcNow.AddDays(-60)),    // thư mục chứng từ khác
        ]);

        var deleted = await Create().RunAsync(UtcNow, default);

        deleted.Should().Be(2);
        Deleted().Should().BeEquivalentTo(Key('c'), Key('e', "PURCHASE_ORDER"));
    }

    [Theory]
    [InlineData("REQUEST/readme.txt")]
    [InlineData("REQUEST/2026/10/short.jpg")]
    [InlineData("REQUEST/2026/10/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.gif")]
    public async Task Never_touches_files_that_are_not_stored_image_keys(string key)
    {
        _files.Add(new(key, UtcNow.AddDays(-60)));

        (await Create().RunAsync(UtcNow, default)).Should().Be(0);

        Deleted().Should().BeEmpty();
    }

    [Fact]
    public async Task Removing_a_row_and_sweeping_the_file_do_not_delete_the_same_file_twice()
    {
        Add(Key('a'), current: false, removedAt: Now.AddDays(-20));
        _files.Add(new(Key('a'), UtcNow.AddDays(-60)));
        var deleted = await Create().RunAsync(UtcNow, default);

        deleted.Should().Be(1);
    }
}
