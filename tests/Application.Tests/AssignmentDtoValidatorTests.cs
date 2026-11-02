using FluentAssertions;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;

namespace Application.Tests;

/// <summary>Giá trị biên của phân công công đoạn: id phải dương, số công đoạn một lần giao từ 1 đến giới hạn, không trùng.</summary>
public class AssignmentDtoValidatorTests
{
    private static List<int> Ids(int count) => Enumerable.Range(1, count).ToList();

    private static bool Many(int orderId, int workerId, List<int> ids) =>
        new AssignOperationsDtoValidator().Validate(new AssignOperationsDto(orderId, workerId, ids)).IsValid;

    [Theory]
    [InlineData(1, 1, 1, true)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, true)]
    [InlineData(0, 1, 1, false)]
    [InlineData(1, 0, 1, false)]
    [InlineData(1, 1, 0, false)]
    [InlineData(-1, 1, 1, false)]
    [InlineData(1, -1, 1, false)]
    [InlineData(1, 1, -1, false)]
    public void AssignOne_RequiresPositiveIds(int orderId, int operationId, int workerId, bool valid) =>
        new AssignOperationDtoValidator().Validate(new AssignOperationDto(orderId, operationId, workerId)).IsValid.Should().Be(valid);

    [Fact]
    public void AssignMany_OperationCountBoundaries()
    {
        Many(1, 1, Ids(0)).Should().BeFalse();
        Many(1, 1, Ids(1)).Should().BeTrue();
        Many(1, 1, Ids(AssignmentLimits.MaxOperations)).Should().BeTrue();
        Many(1, 1, Ids(AssignmentLimits.MaxOperations + 1)).Should().BeFalse();
    }

    [Fact]
    public void AssignMany_RejectsNonPositiveAndDuplicateIds()
    {
        Many(1, 1, [1, 0]).Should().BeFalse();
        Many(1, 1, [1, -2]).Should().BeFalse();
        Many(1, 1, [3, 3]).Should().BeFalse();
        Many(0, 1, [1]).Should().BeFalse();
        Many(1, 0, [1]).Should().BeFalse();
    }

    [Fact]
    public void AssignMany_NullList_IsInvalidWithoutThrowing() =>
        new AssignOperationsDtoValidator().Validate(new AssignOperationsDto(1, 1, null!)).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void Replace_RequiresPositiveWorker(int workerId, bool valid) =>
        new ReplaceAssignmentDtoValidator().Validate(new ReplaceAssignmentDto(workerId)).IsValid.Should().Be(valid);

    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(0, 2, false)]
    [InlineData(1, 0, false)]
    [InlineData(-1, 2, false)]
    public void Copy_RequiresPositiveOrders(int fromOrderId, int toOrderId, bool valid) =>
        new CopyAssignmentsDtoValidator().Validate(new CopyAssignmentsDto(fromOrderId, toOrderId)).IsValid.Should().Be(valid);
}
