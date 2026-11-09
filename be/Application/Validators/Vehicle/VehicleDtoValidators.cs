using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

/// <summary>Trần nhập liệu của xe và chuyến xe, khớp cột DB (DECIMAL(8,2) lít, INT km). FE giữ cùng giá trị ở <c>features/vehicle-log/constants.ts</c>.</summary>
public static class VehicleLimits
{
    public const int MaxOdometer = 9_999_999;
    public const decimal MaxFuelLiters = 1_000m;
    public const decimal MaxFuelCost = 100_000_000m;
    public const decimal MaxFuelNorm = 9_999m;
}

public sealed class SaveVehicleDtoValidator : AbstractValidator<SaveVehicleDto>
{
    public SaveVehicleDtoValidator()
    {
        RuleFor(x => x.PlateNumber).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(WarehouseMessages.PlateRequired)
            .MaximumLength(VehiclePlate.MaxLength)
            .Must(VehiclePlate.IsValid).WithMessage(WarehouseMessages.PlateInvalid);
        RuleFor(x => x.VehicleType).NotEmpty().WithMessage(WarehouseMessages.VehicleTypeRequired).MaximumLength(50);
        RuleFor(x => x.FuelNormPer100Km).Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage(WarehouseMessages.FuelNormInvalid)
            .LessThanOrEqualTo(VehicleLimits.MaxFuelNorm)
            .Must(v => decimal.Round(v, 2) == v).WithMessage(WarehouseMessages.FuelNormScale);
    }
}

public sealed class StartVehicleTripDtoValidator : AbstractValidator<StartVehicleTripDto>
{
    public StartVehicleTripDtoValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.TripDate).NotEmpty();
        RuleFor(x => x.DriverName).NotEmpty().When(x => x.DriverEmployeeId is null).WithMessage(WarehouseMessages.DriverRequired);
        RuleFor(x => x.DriverName).MaximumLength(100);
        RuleFor(x => x.Purpose).MaximumLength(300);
        RuleFor(x => x.OdometerStart).Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.OdometerInvalid)
            .LessThanOrEqualTo(VehicleLimits.MaxOdometer).WithMessage(WarehouseMessages.OdometerTooLarge);
    }
}

public sealed class CompleteVehicleTripDtoValidator : AbstractValidator<CompleteVehicleTripDto>
{
    public CompleteVehicleTripDtoValidator()
    {
        RuleFor(x => x.OdometerEnd).Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage(WarehouseMessages.OdometerInvalid)
            .LessThanOrEqualTo(VehicleLimits.MaxOdometer).WithMessage(WarehouseMessages.OdometerTooLarge);
        // Có xăng thì phải có tiền xăng và ngược lại; không đổ xăng thì để trống cả hai (0 không có nghĩa).
        RuleFor(x => x.FuelLiters)
            .Must(v => v is > 0 and <= VehicleLimits.MaxFuelLiters && decimal.Round(v.Value, 2) == v)
            .WithMessage(WarehouseMessages.FuelLitersInvalid)
            .When(x => x.FuelLiters is not null);
        RuleFor(x => x.FuelCost)
            .Must(v => v is > 0 and <= VehicleLimits.MaxFuelCost && decimal.Truncate(v.Value) == v)
            .WithMessage(WarehouseMessages.FuelCostInvalid)
            .When(x => x.FuelCost is not null);
        RuleFor(x => x).Must(x => (x.FuelLiters is null) == (x.FuelCost is null)).WithName("FuelCost")
            .WithMessage(WarehouseMessages.FuelBothRequired);
    }
}
