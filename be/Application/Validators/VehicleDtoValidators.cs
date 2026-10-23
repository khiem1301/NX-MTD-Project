using FluentValidation;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Validators;

public sealed class SaveVehicleDtoValidator : AbstractValidator<SaveVehicleDto>
{
    public SaveVehicleDtoValidator()
    {
        RuleFor(x => x.PlateNumber).NotEmpty().WithMessage(WarehouseMessages.PlateRequired).MaximumLength(20);
        RuleFor(x => x.VehicleType).NotEmpty().WithMessage(WarehouseMessages.VehicleTypeRequired).MaximumLength(50);
        RuleFor(x => x.FuelNormPer100Km).GreaterThan(0).WithMessage(WarehouseMessages.FuelNormInvalid).LessThanOrEqualTo(9999);
    }
}

public sealed class StartVehicleTripDtoValidator : AbstractValidator<StartVehicleTripDto>
{
    public StartVehicleTripDtoValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.TripDate).NotEmpty();
        RuleFor(x => x.DriverName).NotEmpty().WithMessage(WarehouseMessages.DriverRequired).MaximumLength(100);
        RuleFor(x => x.Purpose).MaximumLength(300);
        RuleFor(x => x.OdometerStart).GreaterThanOrEqualTo(0).WithMessage(WarehouseMessages.OdometerInvalid);
    }
}

public sealed class CompleteVehicleTripDtoValidator : AbstractValidator<CompleteVehicleTripDto>
{
    public CompleteVehicleTripDtoValidator()
    {
        RuleFor(x => x.OdometerEnd).GreaterThan(0).WithMessage(WarehouseMessages.OdometerInvalid);
        RuleFor(x => x.FuelLiters).GreaterThanOrEqualTo(0).When(x => x.FuelLiters is not null).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
        RuleFor(x => x.FuelCost).GreaterThanOrEqualTo(0).When(x => x.FuelCost is not null).WithMessage(WarehouseMessages.QtyMustNotBeNegative);
    }
}
