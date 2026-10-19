using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

public sealed class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithMessage(Messages.UsernameRequired).MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().WithMessage(Messages.PasswordRequired).MaximumLength(100);
    }
}

public sealed class RefreshTokenDtoValidator : AbstractValidator<RefreshTokenDto>
{
    public RefreshTokenDtoValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}
