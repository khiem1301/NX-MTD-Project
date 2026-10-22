using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NX_MTD.API.Filters;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;

namespace Application.Tests;

/// <summary>Bộ lọc kiểm dữ liệu phải trả 400 (không vỡ thành 500) kể cả khi rule ở mức đối tượng, không có tên thuộc tính.</summary>
public class ValidationFilterTests
{
    private static async Task<IActionResult?> RunAsync(object argument)
    {
        var services = new ServiceCollection()
            .AddSingleton<IValidator<CreateAttendanceAdjustmentDto>, CreateAttendanceAdjustmentDtoValidator>()
            .BuildServiceProvider();
        var http = new DefaultHttpContext();
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
            new Dictionary<string, object?> { ["dto"] = argument }, controller: new object());

        await new ValidationFilter(services).OnActionExecutionAsync(context,
            () => Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object())));
        return context.Result;
    }

    [Fact]
    public async Task ObjectLevelRule_ReturnsBadRequestWithItsMessage_InsteadOfThrowing()
    {
        // Hợp lệ mọi trường nhưng không có giờ vào lẫn giờ ra: chỉ rule mức đối tượng báo lỗi.
        var dto = new CreateAttendanceAdjustmentDto(1, new DateOnly(2026, 10, 14), "FORGOT_PUNCH", "Quên quẹt", null, null, null);

        var result = await RunAsync(dto);

        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(400);
        response.Value!.GetType().GetProperty("Message")!.GetValue(response.Value)
            .Should().Be(NX_MTD.Application.Common.Messages.AttendanceAdjustmentNoTime);
    }

    [Fact]
    public async Task ValidDto_ContinuesToTheAction()
    {
        var dto = new CreateAttendanceAdjustmentDto(1, new DateOnly(2026, 10, 14), "FORGOT_PUNCH", "Quên quẹt", null,
            new TimeOnly(7, 30), new TimeOnly(16, 30));

        (await RunAsync(dto)).Should().BeNull();
    }
}
