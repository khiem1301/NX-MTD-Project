using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

public sealed class SaveProductSpecDtoValidator : AbstractValidator<SaveProductSpecDto>
{
    /// <summary>
    /// Số dòng tối đa mỗi lần lưu: một yêu cầu gửi đủ mọi dòng và được ghi trong một giao dịch, nên không để danh sách lớn vô hạn.
    /// MaxNorms tính sau khi mỗi dòng định mức nở ra từng màu – size (thành phần × màu – size × NVL), khớp frontend (productSpec.ts).
    /// </summary>
    public const int MaxComponents = 200;
    public const int MaxVariants = 200;
    public const int MaxNorms = 5000;
    /// <summary>Số nhóm bìa của một mã hàng; số dòng tối đa = số nhóm × số thành phần (chi tiết) hoặc × số màu – size.</summary>
    public const int MaxMarkerNo = 20;
    public const int MaxMarkerRows = MaxMarkerNo * MaxVariants;

    public SaveProductSpecDtoValidator()
    {
        RuleFor(x => x.Components).Cascade(CascadeMode.Stop).NotNull().WithMessage("Thiếu danh sách thành phần.")
            .Must(l => l.Count <= MaxComponents).WithMessage("Tối đa 200 thành phần cho mỗi mã hàng.");
        RuleFor(x => x.Variants).Cascade(CascadeMode.Stop).NotNull().WithMessage("Thiếu danh sách màu – size.")
            .Must(l => l.Count <= MaxVariants).WithMessage("Tối đa 200 màu – size cho mỗi mã hàng.");
        RuleFor(x => x.Norms).Cascade(CascadeMode.Stop).NotNull().WithMessage("Thiếu danh sách định mức.")
            .Must(l => l.Count <= MaxNorms)
            .WithMessage("Tối đa 5.000 dòng định mức (tính theo từng màu – size) cho mỗi lần lưu.");
        // Danh sách quá lớn hoặc thiếu thì không kiểm từng dòng nữa, đã có lỗi ở trên.
        RuleForEach(x => x.Components).ChildRules(c =>
        {
            c.RuleFor(i => i.ComponentName).NotEmpty().WithMessage(Messages.Required("tên thành phần"))
                .MaximumLength(100).WithMessage("Tên thành phần tối đa 100 ký tự.");
            c.RuleFor(i => CodeText.ToCode(i.ComponentType)).Must(t => t is ComponentTypes.Panel or ComponentTypes.Trim)
                .OverridePropertyName("componentType").WithMessage("Loại thành phần phải là Panel hoặc Trim.");
            c.RuleFor(i => i.PiecesPerGarment).InclusiveBetween((short)1, (short)99)
                .WithMessage("Số chi tiết trên một sản phẩm từ 1 đến 99.");
        }).When(x => x.Components is { Count: <= MaxComponents });
        RuleForEach(x => x.Variants).ChildRules(v =>
        {
            v.RuleFor(i => i.Color).NotEmpty().WithMessage(Messages.Required("màu"))
                .MaximumLength(50).WithMessage("Màu tối đa 50 ký tự.");
            v.RuleFor(i => i.SizeCode).NotEmpty().WithMessage(Messages.Required("size"))
                .MaximumLength(10).WithMessage("Size tối đa 10 ký tự.");
        }).When(x => x.Variants is { Count: <= MaxVariants });
        RuleForEach(x => x.Norms).ChildRules(n =>
        {
            n.RuleFor(i => i.ComponentName).NotEmpty().WithMessage("Vui lòng chọn thành phần.");
            n.RuleFor(i => i.Color).NotEmpty().WithMessage(Messages.Required("màu"));
            n.RuleFor(i => i.SizeCode).NotEmpty().WithMessage(Messages.Required("size"));
            n.RuleFor(i => i.MaterialId).GreaterThan(0).WithMessage("Vui lòng chọn NVL.");
            n.RuleFor(i => i.SpecNote).MaximumLength(200).WithMessage("Ghi chú quy cách tối đa 200 ký tự.");
        }).When(x => x.Norms is { Count: <= MaxNorms });
        RuleFor(x => x.MarkerComponents).Cascade(CascadeMode.Stop).NotNull().WithMessage("Thiếu danh sách chi tiết của nhóm bìa.")
            .Must(l => l.Count <= MaxMarkerRows).WithMessage("Danh sách chi tiết của nhóm bìa quá dài.");
        RuleFor(x => x.MarkerFabrics).Cascade(CascadeMode.Stop).NotNull().WithMessage("Thiếu danh sách vải của nhóm bìa.")
            .Must(l => l.Count <= MaxMarkerRows).WithMessage("Danh sách vải của nhóm bìa quá dài.");
        RuleForEach(x => x.MarkerComponents).ChildRules(m =>
        {
            m.RuleFor(i => i.MarkerNo).InclusiveBetween(1, MaxMarkerNo).WithMessage($"Nhóm bìa từ 1 đến {MaxMarkerNo}.");
            m.RuleFor(i => i.ComponentName).NotEmpty().WithMessage("Vui lòng chọn chi tiết của nhóm bìa.")
                .MaximumLength(100).WithMessage("Tên thành phần tối đa 100 ký tự.");
        }).When(x => x.MarkerComponents is { Count: <= MaxMarkerRows });
        RuleForEach(x => x.MarkerFabrics).ChildRules(f =>
        {
            f.RuleFor(i => i.MarkerNo).InclusiveBetween(1, MaxMarkerNo).WithMessage($"Nhóm bìa từ 1 đến {MaxMarkerNo}.");
            f.RuleFor(i => i.Color).NotEmpty().WithMessage(Messages.Required("màu"))
                .MaximumLength(50).WithMessage("Màu tối đa 50 ký tự.");
            f.RuleFor(i => i.MaterialId).GreaterThan(0).WithMessage("Vui lòng chọn vải của nhóm bìa.");
        }).When(x => x.MarkerFabrics is { Count: <= MaxMarkerRows });
    }
}
