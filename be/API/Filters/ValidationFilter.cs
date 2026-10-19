using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NX_MTD.API.Common;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Filters;

/// <summary>Chạy FluentValidation cho mọi argument có validator; sai → 400 + errors theo field (camelCase).</summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;
    public ValidationFilter(IServiceProvider services) => _services = services;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (_services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var failure in result.Errors)
            {
                var key = char.ToLowerInvariant(failure.PropertyName[0]) + failure.PropertyName[1..];
                if (!errors.TryGetValue(key, out var list)) errors[key] = list = new List<string>();
                list.Add(failure.ErrorMessage);
            }
        }

        if (errors.Count > 0)
        {
            var body = ApiResponses.Failure(ErrorCodes.CommonValidation, "Dữ liệu không hợp lệ.",
                errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()));
            context.Result = new ObjectResult(body) { StatusCode = StatusCodes.Status400BadRequest };
            return;
        }
        await next();
    }
}
