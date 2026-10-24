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
    [InlineData(0, 0.0, 0.13)]
    [InlineData(1, 0.0, 0.57)]
    [InlineData(2, 0.0, 0.81)]
    [InlineData(3, 0.0, 0.35)]
    [InlineData(0, 0.25, 0.13 - 1.137 * 0.25 + 1.0)]
    [InlineData(2, 2.0, 0.81 - 2.923 * 2.0 + 6.0)]
    public void TheShimmerPhaseTurnsBackwardsWithTheCyclesAndWrapsInOneTurn(int component, double cycles, double expectedTurns)
    {
        var expected = (expectedTurns - Math.Floor(expectedTurns)) * 2.0 * Math.PI;

        Assert.Equal(expected, MirageSettings.GetShimmerPhase(component, cycles), 1e-5);
    }

    [Theory]
    [InlineData(0, 3.7)]
    [InlineData(1, 120.25)]
    [InlineData(3, 5400.0)]
    public void TheShimmerPhaseStaysInOneTurnAndRepeatsAfterOneCycleOfItsComponent(int component, double cycles)
    {
        var phase = MirageSettings.GetShimmerPhase(component, cycles);
        var repeated = MirageSettings.GetShimmerPhase(component, cycles + 1.0 / MirageSettings.ShimmerCyclesPerSecond[component]);

        Assert.InRange(phase, 0f, (float)(2.0 * Math.PI));
        Assert.Equal(phase, repeated, 1e-3f);
    }

    [Fact]
    public void TheShimmerWeightsAndFrequenciesCoverEveryComponent()
    {
        var count = MirageSettings.ShimmerWeights.Length;

        Assert.Equal(4, count);
        Assert.Equal(count, MirageSettings.ShimmerAzimuthFrequencies.Length);
        Assert.Equal(count, MirageSettings.ShimmerElevationFrequencies.Length);
        Assert.Equal(count, MirageSettings.ShimmerCyclesPerSecond.Length);
        Assert.Equal(count, MirageSettings.ShimmerOffsetCycles.Length);
        Assert.Equal(2.55f, MirageSettings.ShimmerWeights.Sum(), 1e-6f);
        Assert.True(MirageSettings.ShimmerAmplitudeRadians < 0.01);
    }

    [Theory]
    [InlineData(1280.0, 0.0262, 24427.480916)]
    [InlineData(640.0, 0.5773503, 554.256258)]
    [InlineData(64.0, 0.0017453, 18334.4)]
    public void TheFocalLengthIsHalfTheWidthOverTheHalfFieldTangent(double width, double tangent, double expected)
    {
        Assert.Equal(expected, MirageSettings.GetFocalLength(width, tangent), 1.0);
    }

    [Theory]
    [InlineData(0.0, 2000.0)]
    [InlineData(150.0, 2000.0)]
    [InlineData(-640.0, 900.0)]
    [InlineData(4000.0, 87.0)]
    [InlineData(-4000.0, 1663.0)]
    public void ThePitchDirectionTurnsTheCameraByTheAngleOfThePixelsOfTheHorizonPosition(double position, double focal)
    {
        var (cosine, sine) = MirageSettings.GetPitchDirection(position, focal);

        var angle = Math.Atan(position / focal);
        Assert.Equal((float)Math.Cos(angle), cosine, 1e-6f);
        Assert.Equal((float)Math.Sin(angle), sine, 1e-6f);
        Assert.Equal(1f, cosine * cosine + sine * sine, 1e-6f);
        Assert.True(cosine > 0f);
    }

    [Theory]
    [InlineData(120.0, 2000.0)]
    [InlineData(-640.0, 900.0)]
    [InlineData(4000.0, 87.0)]
    public void ThePitchDirectionPutsTheLevelRowAtTheHorizonPosition(double position, double focal)
    {
        var (cosine, sine) = MirageSettings.GetPitchDirection(position, focal);

        Assert.Equal(position, focal * sine / cosine, 1e-2);
    }

    [Fact]
    public void WithoutAHorizonPositionTheCameraLooksLevel()
    {
        Assert.Equal((1f, 0f), MirageSettings.GetPitchDirection(0.0, 1500.0));
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
    }
}
