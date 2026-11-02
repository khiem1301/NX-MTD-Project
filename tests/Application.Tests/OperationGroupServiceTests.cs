using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>DEV-ALL-50: nhóm công đoạn của mã hàng.</summary>
public class OperationGroupServiceTests : ProductionTestBase
{
    private const int ProductId = 1;
    private const int OtherProductId = 2;
    private const int GroupTay = 10;
    private const int GroupCo = 11;
    private const int GroupOther = 20;

    private readonly List<Product> _products = [new() { ProductId = ProductId }, new() { ProductId = OtherProductId }];
    private readonly List<OperationGroup> _groups =
    [
        new() { OperationGroupId = GroupTay, ProductId = ProductId, GroupName = "May tay" },
        new() { OperationGroupId = GroupCo, ProductId = ProductId, GroupName = "May cổ" },
        new() { OperationGroupId = GroupOther, ProductId = OtherProductId, GroupName = "May túi" },
    ];
    private readonly List<Operation> _operations =
    [
        new() { OperationId = 1, ProductId = ProductId, OperationGroupId = GroupTay },
        new() { OperationId = 2, ProductId = ProductId, OperationGroupId = GroupTay, Status = OperationStatuses.Discontinued },
        new() { OperationId = 3, ProductId = ProductId },
        new() { OperationId = 4, ProductId = OtherProductId, OperationGroupId = GroupOther },
    ];
    private readonly OperationGroupService _sut;

    public OperationGroupServiceTests()
    {
        SetupQueryable(_products);
        SetupQueryable(_groups);
        SetupQueryable(_operations);
        _sut = new OperationGroupService(Uow, Audit);
    }

    [Fact]
    public async Task List_OnlyGroupsOfProduct_CountsDiscontinuedAndKeepsEmptyGroups()
    {
        var result = await _sut.ListAsync(ProductId, default);

        result.Data.Should().Equal(new OperationGroupDto(GroupCo, "May cổ", 0), new OperationGroupDto(GroupTay, "May tay", 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task List_InvalidProduct_IsValidationError(int productId)
    {
        (await _sut.ListAsync(productId, default)).Kind.Should().Be(ResultKind.Validation);
    }

    [Fact]
    public async Task Create_TrimsNameAndWritesAudit()
    {
        var result = await _sut.CreateAsync(new CreateOperationGroupDto(ProductId, "  May túi  "), default);

        result.IsSuccess.Should().BeTrue();
        await Repo<OperationGroup>().Received(1).AddAsync(Arg.Is<OperationGroup>(g => g.ProductId == ProductId && g.GroupName == "May túi"));
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(OperationGroup) && e.Action == AuditActions.Insert));
    }

    [Fact]
    public async Task Create_DuplicateNameInSameProduct_IsRejected()
    {
        var result = await _sut.CreateAsync(new CreateOperationGroupDto(ProductId, " May tay "), default);

        result.ErrorCode.Should().Be(ErrorCodes.OperationGroupDuplicate);
        await Repo<OperationGroup>().DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task Create_SameNameInOtherProduct_IsAllowed()
    {
        (await _sut.CreateAsync(new CreateOperationGroupDto(OtherProductId, "May tay"), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Create_ProductMissing_IsNotFound()
    {
        (await _sut.CreateAsync(new CreateOperationGroupDto(99, "May tay"), default)).Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task Rename_ChangesNameAndWritesAudit()
    {
        (await _sut.RenameAsync(GroupTay, new RenameOperationGroupDto(" May tay áo "), default)).IsSuccess.Should().BeTrue();

        _groups.First(g => g.OperationGroupId == GroupTay).GroupName.Should().Be("May tay áo");
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.RecordId == GroupTay && e.Action == AuditActions.Update));
        await Uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Rename_SameName_SavesNothing()
    {
        (await _sut.RenameAsync(GroupTay, new RenameOperationGroupDto("May tay"), default)).IsSuccess.Should().BeTrue();

        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Rename_ToNameOfAnotherGroup_IsRejected()
    {
        var result = await _sut.RenameAsync(GroupTay, new RenameOperationGroupDto("May cổ"), default);

        result.ErrorCode.Should().Be(ErrorCodes.OperationGroupDuplicate);
        _groups.First(g => g.OperationGroupId == GroupTay).GroupName.Should().Be("May tay");
    }

    [Fact]
    public async Task Rename_ToNameUsedOnlyInOtherProduct_IsAllowed()
    {
        (await _sut.RenameAsync(GroupTay, new RenameOperationGroupDto("May túi"), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Rename_Missing_IsNotFound()
    {
        (await _sut.RenameAsync(999, new RenameOperationGroupDto("X"), default)).Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task Delete_DetachesAllMembersIncludingDiscontinued_ThenRemovesGroup()
    {
        (await _sut.DeleteAsync(GroupTay, default)).IsSuccess.Should().BeTrue();

        _operations.Where(o => o.OperationId is 1 or 2).Should().OnlyContain(o => o.OperationGroupId == null);
        _operations.First(o => o.OperationId == 4).OperationGroupId.Should().Be(GroupOther);
        Repo<OperationGroup>().Received(1).RemoveRange(Arg.Is<IEnumerable<OperationGroup>>(g => g.Single().OperationGroupId == GroupTay));
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.RecordId == GroupTay && e.Action == AuditActions.Delete));
    }

    [Fact]
    public async Task Delete_Missing_IsNotFound()
    {
        (await _sut.DeleteAsync(999, default)).Kind.Should().Be(ResultKind.NotFound);
    }

    private Task<Result<int>> SetGroup(int? groupId, params int[] operationIds) =>
        _sut.SetOperationsGroupAsync(new SetOperationsGroupDto(ProductId, operationIds, groupId), default);

    [Fact]
    public async Task SetGroup_MovesOnlyOperationsThatChange_AndReturnsCount()
    {
        var result = await SetGroup(GroupTay, 1, 3);

        result.Data.Should().Be(1);
        _operations.First(o => o.OperationId == 3).OperationGroupId.Should().Be(GroupTay);
        Audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(Operation) && e.RecordId == 3));
        await Uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task SetGroup_NullGroup_Ungroups()
    {
        (await SetGroup(null, 1)).Data.Should().Be(1);

        _operations.First(o => o.OperationId == 1).OperationGroupId.Should().BeNull();
    }

    [Fact]
    public async Task SetGroup_NothingChanges_ReturnsZeroAndSavesNothing()
    {
        (await SetGroup(GroupTay, 1)).Data.Should().Be(0);

        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task SetGroup_GroupOfOtherProduct_IsNotFound()
    {
        (await SetGroup(GroupOther, 1)).Kind.Should().Be(ResultKind.NotFound);

        _operations.First(o => o.OperationId == 1).OperationGroupId.Should().Be(GroupTay);
    }

    [Fact]
    public async Task SetGroup_OperationOfOtherProduct_RejectsWholeRequest()
    {
        var result = await SetGroup(GroupCo, 1, 4);

        result.Message.Should().Be(Messages.OperationGroupProductMismatch);
        await Uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task SetGroup_OperationMissing_RejectsWholeRequest()
    {
        (await SetGroup(GroupCo, 1, 999)).Kind.Should().Be(ResultKind.NotFound);

        await Uow.DidNotReceive().SaveChangesAsync();
    }

    public static TheoryData<int, int[], int?> InvalidSetGroups => new()
    {
        { 0, [1], GroupTay },
        { ProductId, [], GroupTay },
        { ProductId, [0], GroupTay },
        { ProductId, [1, 1], GroupTay },
        { ProductId, [1], 0 },
        { ProductId, Enumerable.Range(1, OperationLimits.MaxOperationsPerGroupChange + 1).ToArray(), GroupTay },
    };

    [Theory]
    [MemberData(nameof(InvalidSetGroups))]
    public void SetGroupValidator_RejectsBoundaryValues(int productId, int[] operationIds, int? groupId) =>
        new SetOperationsGroupDtoValidator().Validate(new SetOperationsGroupDto(productId, operationIds, groupId)).IsValid.Should().BeFalse();

    [Fact]
    public void SetGroupValidator_AcceptsMaxAndNullGroup() =>
        new SetOperationsGroupDtoValidator().Validate(new SetOperationsGroupDto(ProductId,
            Enumerable.Range(1, OperationLimits.MaxOperationsPerGroupChange).ToArray(), null)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("May tay", true)]
    public void GroupNameValidator_RequiresName(string name, bool valid)
    {
        new CreateOperationGroupDtoValidator().Validate(new CreateOperationGroupDto(ProductId, name)).IsValid.Should().Be(valid);
        new RenameOperationGroupDtoValidator().Validate(new RenameOperationGroupDto(name)).IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData(OperationLimits.MaxGroupNameLength, true)]
    [InlineData(OperationLimits.MaxGroupNameLength + 1, false)]
    public void GroupNameValidator_LimitsLength(int length, bool valid) =>
        new RenameOperationGroupDtoValidator().Validate(new RenameOperationGroupDto(new string('x', length))).IsValid.Should().Be(valid);

    private static SaveOperationDto Save(byte? skill = null, decimal? time = null, string? group = null) =>
        new(ProductId, "OP01", "May", 1, skill, time, "Sewing", false, group, null);

    [Theory]
    [InlineData(null, true)]
    [InlineData((byte)0, false)]
    [InlineData((byte)1, true)]
    [InlineData((byte)9, true)]
    [InlineData((byte)10, false)]
    public void SaveOperationValidator_LimitsSkillLevel(byte? skill, bool valid) =>
        new SaveOperationDtoValidator().Validate(Save(skill: skill)).IsValid.Should().Be(valid);

    [Fact]
    public void SaveOperationValidator_LimitsStandardTimeAndGroupName()
    {
        var validator = new SaveOperationDtoValidator();
        validator.Validate(Save(time: OperationLimits.MaxStandardTimeSec)).IsValid.Should().BeTrue();
        validator.Validate(Save(time: OperationLimits.MaxStandardTimeSec + 0.01m)).IsValid.Should().BeFalse();
        validator.Validate(Save(group: new string('x', OperationLimits.MaxGroupNameLength + 1))).IsValid.Should().BeFalse();
    }
}
