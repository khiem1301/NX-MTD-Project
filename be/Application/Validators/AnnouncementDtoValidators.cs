using FluentValidation;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class SendAnnouncementDtoValidator : AbstractValidator<SendAnnouncementDto>
{
    public SendAnnouncementDtoValidator()
    {
        RuleFor(x => x.Content).NotEmpty().WithMessage("Vui lòng nhập nội dung thông báo.");
        RuleFor(x => x.Priority).Must(p => p is AnnouncementPriorities.Normal or AnnouncementPriorities.Urgent)
            .WithMessage("Mức ưu tiên phải là thường hoặc khẩn.");
        RuleFor(x => x.TargetScope).Must(s => s is AnnouncementScopes.All or AnnouncementScopes.Team or AnnouncementScopes.Role)
            .WithMessage("Phạm vi gửi phải là toàn xưởng, theo tổ hoặc theo vai trò.");
    }
}
