namespace Mirage.Tests;

public sealed class MirageTableTests
{
    const double Degrees = 180.0 / Math.PI;
    const double Distance = 40000.0;

    static readonly Lazy<double[]> Standard = new(() => MirageTable.BuildStandard(10.0, Distance));
    static readonly Lazy<float[]> Inferior = new(() => MirageTable.BuildInferior(1.5, 25.0, 0.5));

    static int IndexOf(double degrees)
        => (int)Math.Round((degrees - MirageSettings.MapStartDegrees) / (MirageSettings.MapEndDegrees - MirageSettings.MapStartDegrees) * (MirageSettings.MapWidth - 1));

    static double Angle(int index) => MirageSettings.GetMapAngleRadians(index) * Degrees;

    static double Seen(float[] shifts, double degrees)
    {
        var index = IndexOf(degrees);
        return Angle(index) + shifts[index] * Degrees;
    }

    static double CriticalAngle(float[] shifts)
        => Angle(Array.FindIndex(shifts, shift => shift != 0f));

    [Fact]
    public void ThePhotoOfAnUnchangedAtmosphereIsLeftUnchanged()
    {
        var shifts = MirageTable.BuildSuperior(10.0, 0.0, 60.0, 10.0, Distance, MirageSource.Photo, Standard.Value);

        Assert.True(shifts.Max(Math.Abs) * Degrees < 5e-4);
    }

    [Fact]
    public void TheStandardCoordinateGrowsWithTheApparentAngle()
    {
        var standard = Standard.Value;

        Assert.All(Enumerable.Range(1, standard.Length - 1), index => Assert.True(standard[index] >= standard[index - 1]));
        Assert.True(standard[0] < 0.0);
        Assert.True(standard[^1] > 0.0);
    }

    [Fact]
    public void TheLowestObjectVisibleAtTheDistanceMatchesTheRefractedHorizonFormula()
    {
        var standard = Standard.Value;
        var pressure = MirageSettings.StandardPressure / 100.0;
        var temperature = MirageSettings.StandardTemperature;
        var coefficient = 503.0 * pressure / (temperature * temperature) * (0.0342 - MirageSettings.LapseRate);
        var horizon = Math.Sqrt(2.0 * MirageSettings.EarthRadiusMeters * 10.0 / (1.0 - coefficient));
        var expected = Math.Pow(Distance - horizon, 2.0) * (1.0 - coefficient) / (2.0 * MirageSettings.EarthRadiusMeters);

        var first = standard.First(value => value >= 0.0);

        Assert.Equal(expected, first, 1.5);
        Assert.Equal(49.6, first, 1.0);
    }

    [Fact]
    public void ThePointsBelowTheHorizonAreReportedAsGroundBeforeTheTarget()
    {
        var standard = Standard.Value;

        Assert.All(standard.Where(value => value < 0.0), value => Assert.InRange(value, -Distance, 0.0));
        Assert.Equal(standard.Count(value => value < 0.0), Array.FindIndex(standard, value => value >= 0.0));
    }

    [Fact]
    public void TheInferiorMirageLeavesTheGroundBelowTheCriticalAngleAlone()
    {
        var shifts = Inferior.Value;

        Assert.All(Enumerable.Range(0, IndexOf(-0.38)), index => Assert.Equal(0f, shifts[index]));
        Assert.Equal(-0.3726, CriticalAngle(shifts), 0.005);
    }

    [Theory]
    [InlineData(1.5, 25.0, 0.5)]
    [InlineData(2.0, 25.0, 0.5)]
    [InlineData(2.0, 10.0, 0.5)]
    [InlineData(6.0, 40.0, 1.0)]
    public void TheCriticalAngleIsTheAngleOfTheRayThatJustTouchesTheGround(double eyeHeight, double difference, double thickness)
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Heated, difference, 0.0, thickness);
        var earth = MirageSettings.EarthRadiusMeters;
        var cosine = atmosphere.At(0.0).Index * earth / (atmosphere.At(eyeHeight).Index * (earth + eyeHeight));

        var critical = CriticalAngle(MirageTable.BuildInferior(eyeHeight, difference, thickness));

        Assert.Equal(-Math.Acos(cosine) * Degrees, critical, 0.002);
    }

    [Theory]
    [InlineData(-0.35, 0.355)]
    [InlineData(-0.20, 0.211)]
    [InlineData(-0.05, 0.095)]
    [InlineData(0.0, 0.085)]
    [InlineData(0.10, 0.133)]
    public void TheInferiorMirageShowsTheSkyAboveTheHorizonInsteadOfTheGround(double apparent, double expected)
    {
        Assert.Equal(expected, Seen(Inferior.Value, apparent), 0.002);
    }

    [Theory]
    [InlineData(-0.30)]
    [InlineData(-0.10)]
    [InlineData(0.0)]
    [InlineData(0.20)]
    public void TheInferiorMirageSeesTheDirectionOfTheSameRayInTheUndisturbedAir(double apparent)
    {
        const double EyeHeight = 1.5;
        const double Far = 30000.0;
        var heated = new Atmosphere(AtmosphereLayer.Heated, 25.0, 0.0, 0.5);
        var index = IndexOf(apparent);
        var elevation = RayTracer.Trace(heated, EyeHeight, MirageSettings.GetMapAngleRadians(index), Far, double.PositiveInfinity).Elevation;
        var low = -0.05 / Degrees;
        var high = 1.0 / Degrees;
        for (var step = 0; step < 60; step++)
        {
            var middle = 0.5 * (low + high);
            if (RayTracer.Trace(Atmosphere.Standard, EyeHeight, middle, Far, double.PositiveInfinity).Elevation < elevation)
                low = middle;
            else
                high = middle;
        }

        Assert.Equal(0.5 * (low + high) * Degrees, Seen(Inferior.Value, apparent), 2e-3);
    }

    [Theory]
    [InlineData(2.0, 0.5)]
    [InlineData(20.0, 3.0)]
    public void WithoutAnyTemperatureDifferenceTheInferiorMirageChangesNothing(double eyeHeight, double thickness)
    {
        var shifts = MirageTable.BuildInferior(eyeHeight, 0.0, thickness);

        Assert.All(shifts, shift => Assert.Equal(0f, shift));
    }

    [Fact]
    public void RaysThatBarelyDipIntoTheHotLayerFromAboveAreBarelyShifted()
    {
        var shifts = MirageTable.BuildInferior(20.0, 25.0, 0.5);

        Assert.All(Enumerable.Range(IndexOf(-0.1148), IndexOf(-0.1099) - IndexOf(-0.1148)), index => Assert.True(Math.Abs(shifts[index]) * Degrees < 0.01));
    }

    [Fact]
    public void TheSeenAngleFallsFromTheCriticalAngleToTheLevelDirection()
    {
        var shifts = Inferior.Value;
        var seen = Enumerable.Range(IndexOf(-0.37), IndexOf(-0.01) - IndexOf(-0.37)).Select(index => Angle(index) + shifts[index] * Degrees).ToArray();

        Assert.All(seen.Zip(seen.Skip(1)), pair => Assert.True(pair.First >= pair.Second - 1e-6));
        Assert.True(seen[0] > 0.3);
    }

    [Fact]
    public void TheInferiorMirageOnlyRaisesTheSeenAngle()
    {
        Assert.All(Inferior.Value, shift => Assert.True(shift >= 0f));
    }

    [Fact]
    public void TheInferiorMirageFadesAwayAboveTheLayer()
    {
        var shifts = Inferior.Value;

        Assert.True(shifts[IndexOf(1.0)] * Degrees < 0.01);
        Assert.True(shifts[IndexOf(1.9)] * Degrees < shifts[IndexOf(1.0)] * Degrees);
    }

    [Fact]
    public void AHotterGroundHidesMoreOfTheLandscape()
    {
        var mild = CriticalAngle(MirageTable.BuildInferior(1.5, 10.0, 0.5));
        var hot = CriticalAngle(MirageTable.BuildInferior(1.5, 40.0, 0.5));

        Assert.True(hot < mild);
    }

    [Fact]
    public void ATallerObserverSeesTheMirageFromFurtherBelow()
    {
        var low = CriticalAngle(MirageTable.BuildInferior(1.0, 25.0, 0.5));
        var high = CriticalAngle(MirageTable.BuildInferior(6.0, 25.0, 0.5));

        Assert.True(high < low);
    }

    [Fact]
    public void AThickerHotLayerLeavesLessTemperatureDifferenceBetweenTheEyeAndTheGround()
    {
        var thin = CriticalAngle(MirageTable.BuildInferior(1.5, 25.0, 0.2));
        var thick = CriticalAngle(MirageTable.BuildInferior(1.5, 25.0, 1.5));

        Assert.True(thick > thin);
    }

    [Fact]
    public void RaysThatPassAboveTheHotLayerAreNotShifted()
    {
        var shifts = MirageTable.BuildInferior(20.0, 25.0, 0.1);

        Assert.All(Enumerable.Range(IndexOf(-0.02), IndexOf(2.0) - IndexOf(-0.02)), index => Assert.Equal(0f, shifts[index]));
    }

    [Fact]
    public void TheSuperiorMirageShowsSomeHeightsThreeTimes()
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Inversion, 12.0, 60.0, 10.0);
        var heights = Enumerable.Range(0, MirageSettings.MapWidth)
            .Select(index => MirageTable.Coordinate(RayTracer.Trace(atmosphere, 10.0, MirageSettings.GetMapAngleRadians(index), Distance, double.PositiveInfinity), Distance))
            .ToArray();

        var rising = 0;
        var falling = 0;
        for (var index = 1; index < heights.Length; index++)
        {
            if (heights[index] > heights[index - 1] + 1e-9)
                rising++;
            if (heights[index] < heights[index - 1] - 1e-9)
                falling++;
        }

        Assert.True(falling > 50);
        Assert.True(rising > 500);
        var visible = Enumerable.Range(0, heights.Length).Where(index => heights[index] >= 0.0).ToArray();
        var crossings = 0;
        for (var level = 40.0; level <= 60.0; level += 5.0)
        {
            crossings = Math.Max(crossings, visible.Zip(visible.Skip(1)).Count(pair => (heights[pair.First] - level) * (heights[pair.Second] - level) < 0.0));
        }

        Assert.Equal(3, crossings);
    }

    [Fact]
    public void TheSuperiorMirageLetsTheObserverSeeLowerObjectsThanStandardAirDoes()
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Inversion, 12.0, 60.0, 10.0);
        var lowest = Enumerable.Range(0, MirageSettings.MapWidth)
            .Select(index => MirageTable.Coordinate(RayTracer.Trace(atmosphere, 10.0, MirageSettings.GetMapAngleRadians(index), Distance, double.PositiveInfinity), Distance))
            .Where(value => value >= 0.0)
            .Min();

        Assert.True(lowest < Standard.Value.First(value => value >= 0.0) - 5.0);
    }

    [Fact]
    public void ThePhotoModeClampsTheHiddenPartAtTheHorizon()
    {
        var shifts = MirageTable.BuildSuperior(10.0, 12.0, 60.0, 10.0, Distance, MirageSource.Photo, Standard.Value);
        var horizon = Angle(Array.FindIndex(Standard.Value, value => value >= 0.0));

        Assert.True(Seen(shifts, 0.09) >= horizon - 1e-3);
        Assert.True(Seen(shifts, 0.07) < Seen(shifts, 0.12));
    }

    [Fact]
    public void TheSceneModeSeesTheGeometricElevationOfTheSameObject()
    {
        var photo = MirageTable.BuildSuperior(10.0, 12.0, 60.0, 10.0, Distance, MirageSource.Photo, Standard.Value);
        var scene = MirageTable.BuildSuperior(10.0, 12.0, 60.0, 10.0, Distance, MirageSource.Scene, Standard.Value);
        var unchanged = MirageTable.BuildSuperior(10.0, 0.0, 60.0, 10.0, Distance, MirageSource.Scene, Standard.Value);

        Assert.False(photo.AsSpan().SequenceEqual(scene));
        Assert.All(new[] { -0.3, 0.2, 0.4 }, degrees =>
            Assert.Equal(MirageTable.GeometricElevation(Standard.Value[IndexOf(degrees)], 10.0, Distance) * Degrees, Seen(unchanged, degrees), 1e-4));
    }

    [Theory]
    [InlineData(100.0, 10.0, -0.0008892552)]
    [InlineData(10.0, 10.0, -0.0031392246)]
    [InlineData(0.0, 10.0, -0.0033892236)]
    [InlineData(-10000.0, 10.0, -0.0026877509)]
    public void TheGeometricElevationIsTheStraightLineAngleToTheObjectOnTheSphericalEarth(double coordinate, double eyeHeight, double expected)
    {
        var value = MirageTable.GeometricElevation(coordinate, eyeHeight, 40000.0);

        Assert.Equal(expected, value, 1e-7);
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(10.0)]
    [InlineData(300.0)]
    public void TheGeometricElevationOfTheHorizonPointEqualsTheDipAngle(double eyeHeight)
    {
        var earth = MirageSettings.EarthRadiusMeters;
        var dip = Math.Acos(earth / (earth + eyeHeight));
        var coordinate = earth * dip - 100000.0;

        var value = MirageTable.GeometricElevation(coordinate, eyeHeight, 100000.0);

        Assert.Equal(-dip, value, 1e-12);
    }

    [Fact]
    public void TheStandardElevationInterpolatesBetweenTheMapAngles()
    {
        var standard = new double[MirageSettings.MapWidth];
        for (var index = 0; index < standard.Length; index++)
            standard[index] = index * 2.0;

        Assert.Equal(MirageSettings.GetMapAngleRadians(10), MirageTable.StandardElevation(standard, 20.0), 1e-12);
        Assert.Equal(0.5 * (MirageSettings.GetMapAngleRadians(10) + MirageSettings.GetMapAngleRadians(11)), MirageTable.StandardElevation(standard, 21.0), 1e-12);
        Assert.Equal(MirageSettings.GetMapAngleRadians(0), MirageTable.StandardElevation(standard, -50.0), 1e-12);
        Assert.Equal(MirageSettings.GetMapAngleRadians(MirageSettings.MapWidth - 1), MirageTable.StandardElevation(standard, 1e9), 1e-12);
    }

    [Fact]
    public void AFlatStretchOfTheStandardCoordinateGivesTheLastAngleOfTheStretch()
    {
        var standard = Enumerable.Range(0, MirageSettings.MapWidth).Select(index => index < 1000 ? index * 2.0 : index < 1100 ? 2000.0 : 2000.0 + (index - 1099) * 2.0).ToArray();

        Assert.Equal(MirageSettings.GetMapAngleRadians(1099), MirageTable.StandardElevation(standard, 2000.0), 1e-12);
        Assert.Equal(0.5 * (MirageSettings.GetMapAngleRadians(1099) + MirageSettings.GetMapAngleRadians(1100)), MirageTable.StandardElevation(standard, 2001.0), 1e-12);
    }

    [Theory]
    [InlineData(0, 55.0, 40000.0, 55.0)]
    [InlineData(2, 12.0, 100.0, 12.0)]
    [InlineData(1, 0.0, 30000.0, -10000.0)]
    public void TheCoordinateIsTheHeightForRaysThatReachAndTheMissingDistanceForRaysThatHit(int end, double height, double distance, double expected)
    {
        Assert.Equal(expected, MirageTable.Coordinate(new RayResult((RayEnd)end, height, distance, 0.0), 40000.0), 1e-12);
    }

    [Fact]
    public void ATableIsTheSameForTheSameInputBitForBit()
    {
        var first = MirageTable.BuildSuperior(8.0, 9.0, 80.0, 6.0, 30000.0, MirageSource.Photo, MirageTable.BuildStandard(8.0, 30000.0));
        var second = MirageTable.BuildSuperior(8.0, 9.0, 80.0, 6.0, 30000.0, MirageSource.Photo, MirageTable.BuildStandard(8.0, 30000.0));

        Assert.True(first.AsSpan().SequenceEqual(second));
        Assert.True(MirageTable.BuildInferior(3.0, 30.0, 0.8).AsSpan().SequenceEqual(MirageTable.BuildInferior(3.0, 30.0, 0.8)));
    }

    [Fact]
    public void TheTexelsHoldTheShiftInTheRedChannelWithOpaqueAlpha()
    {
        var shifts = new float[MirageSettings.MapWidth];
        shifts[5] = 0.25f;

        var texels = MirageTable.ToTexels(shifts);

        Assert.Equal(MirageSettings.MapWidth * MirageSettings.TexelChannels, texels.Length);
        Assert.Equal(0.25f, texels[5 * MirageSettings.TexelChannels]);
        Assert.All(Enumerable.Range(0, MirageSettings.MapWidth), index =>
        {
            Assert.Equal(1f, texels[index * MirageSettings.TexelChannels + 3]);
            Assert.Equal(0f, texels[index * MirageSettings.TexelChannels + 1]);
            Assert.Equal(0f, texels[index * MirageSettings.TexelChannels + 2]);
        });
    }
}
