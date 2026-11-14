using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Validators;

/// <summary>Giới hạn bìa của đơn (DEV-ALL-85), khớp cột DB: LengthM DECIMAL(7,2), PiecesPerPly SMALLINT, MarkerCode VARCHAR(20).</summary>
public static class OrderMarkerLimits
{
    public const int MaxMarkers = 50;
    public const int MaxSizes = 30;
    public const decimal MaxLengthM = 99_999.99m;
    public const short MaxPiecesPerPly = 999;
    public const int MaxCodeLength = 20;
}

public sealed class SaveOrderMarkersDtoValidator : AbstractValidator<SaveOrderMarkersDto>
{
    public SaveOrderMarkersDtoValidator()
    {
        RuleFor(x => x.Markers).Cascade(CascadeMode.Stop).NotNull().WithMessage("Thiếu danh sách bìa.")
            .Must(l => l.Count <= OrderMarkerLimits.MaxMarkers)
            .WithMessage($"Mỗi mã hàng của đơn tối đa {OrderMarkerLimits.MaxMarkers} bìa.");
        RuleForEach(x => x.Markers).SetValidator(new SaveOrderMarkerDtoValidator())
            .When(x => x.Markers is { Count: <= OrderMarkerLimits.MaxMarkers });
    }
}

public sealed class SaveOrderMarkerDtoValidator : AbstractValidator<SaveOrderMarkerDto>
{
    public SaveOrderMarkerDtoValidator()
    {
        RuleFor(x => x.MarkerNo).InclusiveBetween(1, SaveProductSpecDtoValidator.MaxMarkerNo)
            .WithMessage($"Nhóm bìa từ 1 đến {SaveProductSpecDtoValidator.MaxMarkerNo}.");
        RuleFor(x => x.MarkerCode).Cascade(CascadeMode.Stop).NotEmpty().WithMessage(Messages.Required("mã bìa"))
            .MaxTrimmedLength(OrderMarkerLimits.MaxCodeLength, "Mã bìa")
            .Matches("^[A-Za-z0-9._-]+$").WithMessage("Mã bìa chỉ gồm chữ, số và . _ -");
        RuleFor(x => x.LengthM).Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("Chiều dài bìa phải lớn hơn 0.")
            .LessThanOrEqualTo(OrderMarkerLimits.MaxLengthM).WithMessage("Chiều dài bìa tối đa 99.999,99 m.")
            .Must(v => decimal.Round(v, 2) == v).WithMessage("Chiều dài bìa tối đa 2 số lẻ.");
        RuleFor(x => x.WidthM).ValidWidthM(WidthLabels.Marker);
        RuleFor(x => CodeText.ToCode(x.Status ?? string.Empty)).Must(s => s is RecordStatus.Active or RecordStatus.Discontinued)
            .OverridePropertyName("status").WithMessage("Trạng thái bìa không hợp lệ.");
        RuleFor(x => x.Ratios).Cascade(CascadeMode.Stop).NotNull().WithMessage("Bìa phải có tỉ lệ size.")
            .Must(l => l.Count > 0).WithMessage("Bìa phải có ít nhất một size trong tỉ lệ.")
            .Must(l => l.Count <= OrderMarkerLimits.MaxSizes).WithMessage($"Tỉ lệ tối đa {OrderMarkerLimits.MaxSizes} size.")
            .Must(l => l.Select(r => r.SizeCode?.Trim().ToUpperInvariant()).Distinct().Count() == l.Count)
            .WithMessage("Mỗi size chỉ khai một lần trong tỉ lệ bìa.");
        RuleForEach(x => x.Ratios).ChildRules(r =>
        {
            r.RuleFor(i => i.SizeCode).NotEmpty().WithMessage(Messages.Required("size"))
                .MaximumLength(10).WithMessage("Size tối đa 10 ký tự.");
            r.RuleFor(i => i.PiecesPerPly).InclusiveBetween((short)1, OrderMarkerLimits.MaxPiecesPerPly)
                .WithMessage($"Số sản phẩm trên một lớp từ 1 đến {OrderMarkerLimits.MaxPiecesPerPly}.");
        }).When(x => x.Ratios is { Count: > 0 and <= OrderMarkerLimits.MaxSizes });
    }
}
