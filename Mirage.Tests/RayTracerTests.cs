namespace Mirage.Tests;

public sealed class RayTracerTests
{
    const double Degrees = 180.0 / Math.PI;
    const double Earth = MirageSettings.EarthRadiusMeters;

    static double Invariant(Atmosphere atmosphere, double height, double elevation)
        => atmosphere.At(height).Index * (Earth + height) * Math.Cos(elevation);

    static double GrazingElevation(Atmosphere atmosphere, double eyeHeight, double distance)
    {
        var low = -1.0 / Degrees;
        var high = 0.0;
        for (var step = 0; step < 60; step++)
        {
            var middle = 0.5 * (low + high);
            if (RayTracer.Trace(atmosphere, eyeHeight, middle, distance, double.PositiveInfinity).End == RayEnd.Hit)
                low = middle;
            else
                high = middle;
        }

        return 0.5 * (low + high);
    }

    [Theory]
    [InlineData(0.0, 10.0, 12000.0)]
    [InlineData(0.01, 2.0, 40000.0)]
    [InlineData(-0.01, 30.0, 25000.0)]
    [InlineData(0.002, 1000.0, 100000.0)]
    public void InAVacuumARayFollowsAStraightLineAcrossTheCurvedEarth(double elevationDegrees, double eyeHeight, double distance)
    {
        var elevation = elevationDegrees / Degrees;
        var angle = distance / Earth;
        var start = Earth + eyeHeight;
        var length = start * Math.Tan(angle) / (Math.Cos(elevation) - Math.Sin(elevation) * Math.Tan(angle));
        var expected = Math.Sqrt(Math.Pow(length * Math.Cos(elevation), 2.0) + Math.Pow(start + length * Math.Sin(elevation), 2.0)) - Earth;

        var result = RayTracer.Trace(Atmosphere.Vacuum, eyeHeight, elevation, distance, double.PositiveInfinity);

        Assert.Equal(RayEnd.Reached, result.End);
        Assert.Equal(expected, result.Height, 1e-4);
        Assert.Equal(distance, result.Distance, 1e-3);
    }

    [Fact]
    public void InAVacuumTheHorizonDipIsTheGeometricDip()
    {
        const double EyeHeight = 10.0;

        var dip = GrazingElevation(Atmosphere.Vacuum, EyeHeight, 400000.0);

        Assert.Equal(-Math.Acos(Earth / (Earth + EyeHeight)), dip, 1e-6);
    }

    [Fact]
    public void ThroughStandardAirTheHorizonDipIsShallowerByTheRefractionCoefficient()
    {
        const double EyeHeight = 10.0;
        var pressure = MirageSettings.StandardPressure / 100.0;
        var temperature = MirageSettings.StandardTemperature;
        var coefficient = 503.0 * pressure / (temperature * temperature) * (0.0342 - MirageSettings.LapseRate);
        var expected = Math.Acos(Earth / (Earth + EyeHeight)) * Math.Sqrt(1.0 - coefficient);

        var dip = GrazingElevation(Atmosphere.Standard, EyeHeight, 400000.0);

        Assert.Equal(-expected, dip, 4e-6);
        Assert.Equal(-0.0925, dip * Degrees, 5e-4);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.05)]
    [InlineData(-0.04)]
    public void TheBouguerInvariantIsConservedAlongTheRay(double elevationDegrees)
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Inversion, 12.0, 60.0, 10.0);
        var start = Invariant(atmosphere, 10.0, elevationDegrees / Degrees);

        var result = RayTracer.Trace(atmosphere, 10.0, elevationDegrees / Degrees, 40000.0, double.PositiveInfinity);

        Assert.Equal(RayEnd.Reached, result.End);
        Assert.Equal(start, Invariant(atmosphere, result.Height, result.Elevation), start * 1e-10);
    }

    [Fact]
    public void TheBouguerInvariantIsConservedThroughAThinHeatedLayer()
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Heated, 25.0, 0.0, 0.5);
        var elevation = -0.2 / Degrees;
        var start = Invariant(atmosphere, 1.5, elevation);

        var result = RayTracer.Trace(atmosphere, 1.5, elevation, double.PositiveInfinity, 5.0);

        Assert.Equal(RayEnd.Exited, result.End);
        Assert.Equal(5.0, result.Height, 1e-9);
        Assert.Equal(start, Invariant(atmosphere, result.Height, result.Elevation), start * 1e-7);
    }

    [Fact]
    public void ASteepRayDownAtTheGroundReportsWhereItMeetsTheGround()
    {
        var result = RayTracer.Trace(Atmosphere.Standard, 10.0, -1.0 / Degrees, 40000.0, double.PositiveInfinity);

        Assert.Equal(RayEnd.Hit, result.End);
        Assert.Equal(0.0, result.Height);
        Assert.Equal(10.0 / Math.Tan(1.0 / Degrees), result.Distance, 3.0);
        Assert.True(result.Elevation < 0.0);
    }

    [Fact]
    public void ARayAboveTheHorizonReachesTheTargetDistanceAtAPositiveHeight()
    {
        var result = RayTracer.Trace(Atmosphere.Standard, 10.0, 0.5 / Degrees, 40000.0, double.PositiveInfinity);

        Assert.Equal(RayEnd.Reached, result.End);
        Assert.Equal(40000.0, result.Distance, 1e-3);
        Assert.InRange(result.Height, 450.0, 480.0);
    }

    [Fact]
    public void ALevelRayLeavesTheEarthBecauseTheGroundCurvesAway()
    {
        var result = RayTracer.Trace(Atmosphere.Standard, 10.0, 0.0, 40000.0, double.PositiveInfinity);

        Assert.Equal(RayEnd.Reached, result.End);
        Assert.InRange(result.Height, 105.0, 125.0);
    }

    [Fact]
    public void ARayStartingAboveTheCeilingAndGoingUpExitsAtOnce()
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Heated, 25.0, 0.0, 0.5);

        var result = RayTracer.Trace(atmosphere, 20.0, 0.1 / Degrees, double.PositiveInfinity, 5.0);

        Assert.Equal(RayEnd.Exited, result.End);
        Assert.Equal(0.1 / Degrees, result.Elevation, 1e-12);
        Assert.Equal(0.0, result.Distance);
    }

    [Fact]
    public void ARayEnteringAHeatedLayerAtAShallowAngleTurnsUpAndLeaves()
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Heated, 25.0, 0.0, 0.5);

        var result = RayTracer.Trace(atmosphere, 1.5, -0.2 / Degrees, double.PositiveInfinity, 5.0);

        Assert.Equal(RayEnd.Exited, result.End);
        Assert.True(result.Elevation > 0.0);
        Assert.True(result.Distance > 200.0);
    }

    [Fact]
    public void ARayEnteringAHeatedLayerSteeplyHitsTheGround()
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Heated, 25.0, 0.0, 0.5);

        var result = RayTracer.Trace(atmosphere, 1.5, -0.6 / Degrees, double.PositiveInfinity, 5.0);

        Assert.Equal(RayEnd.Hit, result.End);
    }

    [Fact]
    public void AnInversionLayerCarriesTheRaysLowerAtTheTargetThanStandardAirDoesForTheSameAngle()
    {
        var inversion = new Atmosphere(AtmosphereLayer.Inversion, 12.0, 60.0, 10.0);

        var standard = RayTracer.Trace(Atmosphere.Standard, 10.0, 0.08 / Degrees, 40000.0, double.PositiveInfinity);
        var bent = RayTracer.Trace(inversion, 10.0, 0.08 / Degrees, 40000.0, double.PositiveInfinity);

        Assert.True(bent.Height < standard.Height);
    }

    [Fact]
    public void TheSameRayGivesTheSameResultEveryTime()
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Inversion, 12.0, 60.0, 10.0);

        var first = RayTracer.Trace(atmosphere, 10.0, 0.01 / Degrees, 40000.0, double.PositiveInfinity);
        var second = RayTracer.Trace(atmosphere, 10.0, 0.01 / Degrees, 40000.0, double.PositiveInfinity);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ARayThatNeverMeetsTheCeilingNorTheGroundStopsAtTheLongestDistance()
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Heated, 25.0, 0.0, 0.5);

        var result = RayTracer.Trace(atmosphere, 30.0, -0.0005 / Degrees, double.PositiveInfinity, 5.0);

        Assert.Equal(RayEnd.Passed, result.End);
        Assert.True(result.Distance >= MirageSettings.MaximumRayDistanceMeters);
    }
}
