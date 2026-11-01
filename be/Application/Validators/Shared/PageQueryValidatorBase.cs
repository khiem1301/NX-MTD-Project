using FluentValidation;
using NX_MTD.Application.Common;

namespace NX_MTD.Application.Validators;

/// <summary>Validator của từng query kế thừa class này để có sẵn luật trang.</summary>
public abstract class PageQueryValidatorBase<T> : AbstractValidator<T> where T : PageQuery
{
    protected PageQueryValidatorBase()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageQuery.MaxPageSize);
        RuleFor(x => x.Keyword).MaximumLength(100);
    }
}
