using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

internal static class HrRules
{
    public static readonly string[] PayTypes = { "PieceRate", "Daily", "Monthly" };
    public static readonly string[] TeamTypeNames = { "Cutting", "Sewing", "Office" };
    public static readonly string[] Positions = { TeamPositions.Leader, TeamPositions.Deputy };

    public static bool IsKnownRole(string role) => Roles.IsKnown(role);
    public static bool NeedsTeam(string role) => role is Roles.TeamLeader or Roles.TeamDeputy or Roles.Worker;
}

public sealed class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage(Messages.Required("mật khẩu hiện tại"));
        RuleFor(x => x.NewPassword).NotEmpty().WithMessage(Messages.Required("mật khẩu mới")).MaximumLength(100);
        RuleFor(x => x.ConfirmPassword).NotEmpty().WithMessage(Messages.Required("mật khẩu nhập lại"))
            .Equal(x => x.NewPassword).WithMessage(Messages.PasswordMismatch);   // MSG05
    }
}

public sealed class CreateEmployeeDtoValidator : AbstractValidator<CreateEmployeeDto>
{
    public CreateEmployeeDtoValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().WithMessage(Messages.Required("mã nhân viên")).MaximumLength(20)
            .Must(c => System.Text.RegularExpressions.Regex.IsMatch(c.Trim(), "^[A-Za-z0-9._-]+$"))
            .WithMessage("Mã nhân viên chỉ gồm chữ, số và . _ -");
        RuleFor(x => x.FullName).NotEmpty().WithMessage(Messages.Required("họ tên")).MaximumLength(100);
        RuleFor(x => x.Gender).Must(g => g is "M" or "F").When(x => x.Gender is not null).WithMessage("Giới tính chỉ nhận M hoặc F.");
        RuleFor(x => x.DateOfBirth).LessThan(DateOnly.FromDateTime(DateTime.Today)).When(x => x.DateOfBirth is not null);
        RuleFor(x => x.Phone).MaximumLength(15).Matches("^[0-9+]*$").WithMessage("Số điện thoại chỉ gồm chữ số và dấu +.");
        RuleFor(x => x.Role).Must(HrRules.IsKnownRole).WithMessage("Vai trò không hợp lệ.");
        RuleFor(x => x.PayType).Must(HrRules.PayTypes.Contains).WithMessage("Kiểu lương không hợp lệ.");
        RuleFor(x => x.AttendanceCode).InclusiveBetween(1, 99_999_999).When(x => x.AttendanceCode is not null);
        RuleFor(x => x.TeamId).NotNull().When(x => HrRules.NeedsTeam(x.Role)).WithMessage(Messages.Required("tổ"));
    }
}

public sealed class AdminUpdateEmployeeDtoValidator : AbstractValidator<AdminUpdateEmployeeDto>
{
    public AdminUpdateEmployeeDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage(Messages.Required("họ tên")).MaximumLength(100);
        RuleFor(x => x.Gender).Must(g => g is "M" or "F").When(x => x.Gender is not null).WithMessage("Giới tính chỉ nhận M hoặc F.");
        RuleFor(x => x.Phone).MaximumLength(15).Matches("^[0-9+]*$").WithMessage("Số điện thoại chỉ gồm chữ số và dấu +.");
        RuleFor(x => x.Role).Must(HrRules.IsKnownRole).WithMessage("Vai trò không hợp lệ.");
        RuleFor(x => x.PayType).Must(HrRules.PayTypes.Contains).WithMessage("Kiểu lương không hợp lệ.");
        RuleFor(x => x.AttendanceCode).InclusiveBetween(1, 99_999_999).When(x => x.AttendanceCode is not null);
        RuleFor(x => x.TeamId).NotNull().When(x => HrRules.NeedsTeam(x.Role)).WithMessage(Messages.Required("tổ"));
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class ManagerUpdateEmployeeDtoValidator : AbstractValidator<ManagerUpdateEmployeeDto>
{
    public ManagerUpdateEmployeeDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage(Messages.Required("họ tên")).MaximumLength(100);
        RuleFor(x => x.Gender).Must(g => g is "M" or "F").When(x => x.Gender is not null).WithMessage("Giới tính chỉ nhận M hoặc F.");
        RuleFor(x => x.Phone).MaximumLength(15).Matches("^[0-9+]*$").WithMessage("Số điện thoại chỉ gồm chữ số và dấu +.");
        RuleFor(x => x.AttendanceCode).InclusiveBetween(1, 99_999_999).When(x => x.AttendanceCode is not null);
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class ResignEmployeeDtoValidator : AbstractValidator<ResignEmployeeDto>
{
    public ResignEmployeeDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage(Messages.ReasonRequired).MaximumLength(500);   // MSG09
        RuleFor(x => x.RowVer).NotEmpty();
    }
}

public sealed class LockAccountDtoValidator : AbstractValidator<LockAccountDto>
{
    public LockAccountDtoValidator() =>
        RuleFor(x => x.Reason).NotEmpty().WithMessage(Messages.ReasonRequired).MaximumLength(300);   // MSG09
}

public sealed class SaveTeamDtoValidator : AbstractValidator<SaveTeamDto>
{
    public SaveTeamDtoValidator()
    {
        RuleFor(x => x.TeamCode).NotEmpty().WithMessage(Messages.Required("mã tổ")).MaximumLength(20)
            .Must(c => System.Text.RegularExpressions.Regex.IsMatch(c.Trim(), "^[A-Za-z0-9._-]+$"))
            .WithMessage("Mã tổ chỉ gồm chữ, số và . _ -");
        RuleFor(x => x.TeamName).NotEmpty().WithMessage(Messages.Required("tên tổ")).MaximumLength(100);
        RuleFor(x => x.TeamType).Must(HrRules.TeamTypeNames.Contains).WithMessage("Loại tổ không hợp lệ.");
    }
}

public sealed class TransferMembersDtoValidator : AbstractValidator<TransferMembersDto>
{
    public TransferMembersDtoValidator()
    {
        RuleFor(x => x.EmployeeIds).NotEmpty().Must(l => l.Count <= 200).WithMessage("Tối đa 200 nhân viên.")
            .Must(l => l.Distinct().Count() == l.Count).WithMessage("Danh sách nhân viên bị trùng.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class AppointDtoValidator : AbstractValidator<AppointDto>
{
    public AppointDtoValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.Position).Must(HrRules.Positions.Contains).WithMessage("Chức vụ chỉ nhận Leader hoặc Deputy.");
    }
}

public sealed class EmployeeQueryValidator : PageQueryValidatorBase<EmployeeQuery> { }
