using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Phiếu hàng hỏng quá 48 giờ không bị phản đối tự chuyển sang CONFIRMED.</summary>
public class DefectAutoConfirmServiceTests : ProductionTestBase
{
    private readonly List<DefectRecord> _defects = [];
    private readonly DefectAutoConfirmService _sut;

    public DefectAutoConfirmServiceTests()
    {
        SetupQueryable(_defects);
        Uow.SaveChangesAsync().Returns(Task.FromResult(1));
        _sut = new DefectAutoConfirmService(Uow, Clock, NullLogger<DefectAutoConfirmService>.Instance);
    }

    private DefectRecord Add(int minutesAgo, string status = DefectStatuses.Recorded, string? objection = null)
    {
        var defect = new DefectRecord
        {
            DefectRecordId = _defects.Count + 1, Status = status, WorkerObjection = objection,
            RecordedAt = Now.AddMinutes(-minutesAgo),
        };
        _defects.Add(defect);
        return defect;
    }

    [Fact]
    public async Task Run_ConfirmsOnlyUnobjectedRecordedDefectsPast48Hours()
    {
        var old = Add(48 * 60 + 1);
        var exactly = Add(48 * 60);
        var fresh = Add(60);
        var objected = Add(72 * 60, objection: "Không phải lỗi của em");
        var cancelled = Add(72 * 60, status: DefectStatuses.Cancelled);

        var count = await _sut.RunAsync(default);

        count.Should().Be(1);
        old.Status.Should().Be(DefectStatuses.Confirmed);
        old.UpdatedAt.Should().Be(Now);
        new[] { exactly, fresh, objected }.Should().OnlyContain(d => d.Status == DefectStatuses.Recorded);
        cancelled.Status.Should().Be(DefectStatuses.Cancelled);
    }

    [Fact]
    public async Task Run_WithNothingDue_DoesNotSave()
    {
        Add(60);

        (await _sut.RunAsync(default)).Should().Be(0);
        await Uow.DidNotReceive().SaveChangesAsync();
    }
}
