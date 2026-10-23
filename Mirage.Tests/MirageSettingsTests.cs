namespace Mirage.Tests;

public sealed class MirageSettingsTests
{
    const double Degrees = 180.0 / Math.PI;

    [Fact]
    public void TheMapSpansFromMinusTwoToPlusTwoDegrees()
    {
        Assert.Equal(-2.0, MirageSettings.GetMapAngleRadians(0) * Degrees, 1e-9);
        Assert.Equal(2.0, MirageSettings.GetMapAngleRadians(MirageSettings.MapWidth - 1) * Degrees, 1e-9);
        Assert.Equal(4.0, MirageSettings.GetMapSpanRadians() * Degrees, 1e-9);
        Assert.Equal(-2.0, MirageSettings.GetMapStartRadians() * Degrees, 1e-9);
    }

    [Fact]
    public void TheMapAnglesAreEvenlySpaced()
    {
        var step = MirageSettings.GetMapSpanRadians() / (MirageSettings.MapWidth - 1);

        for (var index = 1; index < MirageSettings.MapWidth; index += 97)
            Assert.Equal(step, MirageSettings.GetMapAngleRadians(index) - MirageSettings.GetMapAngleRadians(index - 1), 1e-12);
    }

    [Theory]
    [InlineData(60.0, 0.5773503f)]
    [InlineData(1.0, 0.0087269f)]
    [InlineData(0.2, 0.0017453f)]
    [InlineData(0.01, 0.0017453f)]
    [InlineData(120.0, 0.5773503f)]
    public void TheFieldOfViewIsHeldBetweenTwoTenthsAndSixtyDegrees(double field, float expected)
    {
        Assert.Equal(expected, MirageSettings.GetHalfFieldOfViewTangent(field), 1e-6f);
    }

    [Fact]
    public void TheMapReachesBelowTheCriticalAngleOfTheLargestHeatedLayer()
    {
        var shifts = MirageTable.BuildInferior(MirageSettings.MaximumEyeHeightMeters, MirageSettings.MaximumHeatDifference, MirageSettings.MaximumHeatThicknessMeters);

        var first = Array.FindIndex(shifts, shift => shift != 0f);

        Assert.True(first > MirageSettings.MapWidth / 20);
    }

    [Fact]
    public void EveryRangeHasItsMinimumBelowItsMaximum()
    {
        Assert.True(MirageSettings.MinimumEyeHeightMeters < MirageSettings.MaximumEyeHeightMeters);
        Assert.True(MirageSettings.MinimumHeatDifference < MirageSettings.MaximumHeatDifference);
        Assert.True(MirageSettings.MinimumHeatThicknessMeters < MirageSettings.MaximumHeatThicknessMeters);
        Assert.True(MirageSettings.MinimumInversionDifference < MirageSettings.MaximumInversionDifference);
        Assert.True(MirageSettings.MinimumInversionHeightMeters < MirageSettings.MaximumInversionHeightMeters);
        Assert.True(MirageSettings.MinimumInversionThicknessMeters < MirageSettings.MaximumInversionThicknessMeters);
        Assert.True(MirageSettings.MinimumDistanceKilometers < MirageSettings.MaximumDistanceKilometers);
        Assert.True(MirageSettings.MinimumFieldOfViewDegrees < MirageSettings.MaximumFieldOfViewDegrees);
        Assert.True(MirageSettings.MinimumCameraPitchDegrees < MirageSettings.MaximumCameraPitchDegrees);
    }
}
