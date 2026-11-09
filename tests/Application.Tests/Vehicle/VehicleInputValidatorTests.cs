using FluentAssertions;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;

namespace Application.Tests;

/// <summary>Biển số, số xăng, tiền xăng, số km và định mức xăng của xe / chuyến xe.</summary>
public class VehicleInputValidatorTests
{
    private static bool PlateValid(string plate) => new SaveVehicleDtoValidator().Validate(new SaveVehicleDto(plate, "Xe tải", 12m)).IsValid;

    private static bool TripValid(decimal? liters, decimal? cost, int odometerEnd = 1200) =>
        new CompleteVehicleTripDtoValidator().Validate(new CompleteVehicleTripDto(odometerEnd, liters, cost)).IsValid;

    [Theory]
    [InlineData("51C-123.45", true)]
    [InlineData("51d-678.90", true)]     // chữ thường được chuẩn hóa
    [InlineData(" 29LD-123.45 ", true)]
    [InlineData("30F-1234", true)]
    [InlineData("29C-12345", true)]
    [InlineData("29C-1", false)]         // thiếu số
    [InlineData("51C123.45", false)]     // thiếu gạch ngang
    [InlineData("5C-123.45", false)]     // mã tỉnh phải 2 số
    [InlineData("01C-123.45", false)]    // mã tỉnh không bắt đầu bằng 0
    [InlineData("51C-12.345", false)]    // dấu chấm sai chỗ
    [InlineData("51C-123.456", false)]
    [InlineData("ABC-123.45", false)]
    [InlineData("51C-123.45 !", false)]
    public void Plate_MustLookLikeAVietnameseCarPlate(string plate, bool valid) => PlateValid(plate).Should().Be(valid);

    [Theory]
    [InlineData(0.5, 100_000, true)]
    [InlineData(1_000, 100_000_000, true)]      // đúng trần
    [InlineData(0, 100_000, false)]             // 0 lít không có nghĩa
    [InlineData(1_000.01, 100_000, false)]      // vượt 1.000 lít
    [InlineData(10.123, 100_000, false)]        // quá 2 số lẻ
    [InlineData(10, 0, false)]                  // 0 đồng
    [InlineData(10, 100_000_001, false)]        // vượt trần tiền
    [InlineData(10, 1000.5, false)]             // tiền xăng phải là số nguyên đồng
    public void FuelLitersAndCost_AreBoundedAndPositive(double liters, double cost, bool valid) =>
        TripValid((decimal)liters, (decimal)cost).Should().Be(valid);

    [Fact]
    public void FuelLitersAndCost_AreBothGivenOrBothEmpty()
    {
        TripValid(null, null).Should().BeTrue();
        TripValid(10, null).Should().BeFalse();
        TripValid(null, 100_000).Should().BeFalse();
    }

    [Fact]
    public void Odometer_HasAnUpperBound()
    {
        TripValid(null, null, odometerEnd: 9_999_999).Should().BeTrue();
        TripValid(null, null, odometerEnd: 10_000_000).Should().BeFalse();
        new StartVehicleTripDtoValidator().Validate(new StartVehicleTripDto(1, new DateOnly(2026, 11, 7), "An", null, null, null, 10_000_000))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void FuelNorm_AtMostTwoDecimals()
    {
        var validator = new SaveVehicleDtoValidator();
        validator.Validate(new SaveVehicleDto("51C-123.45", "Xe tải", 12.5m)).IsValid.Should().BeTrue();
        validator.Validate(new SaveVehicleDto("51C-123.45", "Xe tải", 12.555m)).IsValid.Should().BeFalse();
        validator.Validate(new SaveVehicleDto("51C-123.45", "Xe tải", 10_000m)).IsValid.Should().BeFalse();
    }
}
