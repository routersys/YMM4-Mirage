namespace Mirage.Tests;

public sealed class AirOpticsTests
{
    static double Slope(Func<double, double> function, double height, double step = 1e-3)
        => (function(height + step) - function(height - step)) / (2.0 * step);

    [Theory]
    [InlineData(400.0, 0.0002827618234821789)]
    [InlineData(500.0, 0.0002789738106021295)]
    [InlineData(550.0, 0.00027783763542930603)]
    [InlineData(633.0, 0.0002765302104355886)]
    [InlineData(700.0, 0.0002758044985039053)]
    public void TheRefractivityOfStandardAirFollowsCiddorsEquation(double wavelength, double expected)
    {
        Assert.Equal(expected, AirOptics.StandardRefractivity(wavelength), 1e-15);
    }

    [Fact]
    public void TheRefractivityOfStandardAirAtHeNeWavelengthMatchesThePublishedValue()
    {
        Assert.Equal(2.7653e-4, AirOptics.StandardRefractivity(633.0), 1e-8);
    }

    [Fact]
    public void ShorterWavelengthsAreRefractedMore()
    {
        var values = Enumerable.Range(0, 40).Select(step => AirOptics.StandardRefractivity(400.0 + step * 10.0)).ToArray();

        Assert.All(values.Zip(values.Skip(1)), pair => Assert.True(pair.First > pair.Second));
    }

    [Fact]
    public void TheRefractivityIsProportionalToTheDensity()
    {
        var standard = AirOptics.StandardRefractivity(550.0);

        Assert.Equal(standard, AirOptics.Refractivity(550.0, MirageSettings.StandardTemperature, MirageSettings.StandardPressure), 1e-18);
        Assert.Equal(standard * (95000.0 / 101325.0) * (288.15 / 303.15), AirOptics.Refractivity(550.0, 303.15, 95000.0), 1e-15);
        Assert.Equal(0.0002476048335686308, AirOptics.Refractivity(550.0, 303.15, 95000.0), 1e-15);
    }

    [Fact]
    public void TheStandardAtmosphereStartsAtTheInternationalStandardValues()
    {
        var atmosphere = Atmosphere.Standard;

        Assert.Equal(288.15, atmosphere.Temperature(0.0), 1e-12);
        Assert.Equal(101325.0, atmosphere.Pressure(0.0), 1e-9);
    }

    [Fact]
    public void TheStandardAtmosphereFallsAtTheStandardLapseRate()
    {
        var atmosphere = Atmosphere.Standard;

        Assert.Equal(281.65, atmosphere.Temperature(1000.0), 1e-9);
        Assert.Equal(89874.56253218028, atmosphere.Pressure(1000.0), 1e-6);
        Assert.Equal(-0.0065, atmosphere.TemperatureGradient(0.0), 1e-12);
    }

    [Fact]
    public void ThePressureGradientIsTheSlopeOfThePressure()
    {
        var atmosphere = Atmosphere.Standard;

        foreach (var height in new[] { 0.0, 10.0, 500.0, 3000.0 })
            Assert.Equal(Slope(atmosphere.Pressure, height), atmosphere.PressureGradient(height), 1e-3);
    }

    [Fact]
    public void TheInversionLayerAddsHalfItsDifferenceAtItsCenterAndTheWholeAboveIt()
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Inversion, 12.0, 60.0, 10.0);
        var standard = Atmosphere.Standard;

        Assert.Equal(6.0, atmosphere.Temperature(60.0) - standard.Temperature(60.0), 1e-12);
        Assert.Equal(12.0, atmosphere.Temperature(400.0) - standard.Temperature(400.0), 1e-6);
        Assert.Equal(0.0, atmosphere.Temperature(0.0) - standard.Temperature(0.0), 1e-2);
    }

    [Fact]
    public void TheInversionLayerRaisesTheTemperatureAtTheLayerHeightByTheGradientOverItsThickness()
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Inversion, 12.0, 60.0, 10.0);

        Assert.Equal(-0.0065 + 12.0 * 0.5 / 10.0, atmosphere.TemperatureGradient(60.0), 1e-12);
        Assert.Equal(-0.0065, atmosphere.TemperatureGradient(1000.0), 1e-9);
    }

    [Fact]
    public void TheHeatedLayerAddsItsDifferenceAtTheGroundAndLosesItWithHeight()
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Heated, 25.0, 0.0, 0.5);
        var standard = Atmosphere.Standard;

        Assert.Equal(25.0, atmosphere.Temperature(0.0) - standard.Temperature(0.0), 1e-12);
        Assert.Equal(25.0 / Math.E, atmosphere.Temperature(0.5) - standard.Temperature(0.5), 1e-12);
        Assert.Equal(0.0, atmosphere.Temperature(30.0) - standard.Temperature(30.0), 1e-9);
        Assert.Equal(-0.0065 - 25.0 / 0.5, atmosphere.TemperatureGradient(0.0), 1e-12);
    }

    [Theory]
    [InlineData(1, 12.0, 60.0, 10.0)]
    [InlineData(2, 25.0, 0.0, 0.5)]
    [InlineData(0, 0.0, 0.0, 0.0)]
    public void TheTemperatureGradientIsTheSlopeOfTheTemperature(int layer, double difference, double height, double thickness)
    {
        var atmosphere = new Atmosphere((AtmosphereLayer)layer, difference, height, thickness);

        foreach (var sample in new[] { 0.1, 0.4, 5.0, 55.0, 60.0, 66.0, 300.0 })
            Assert.Equal(Slope(atmosphere.Temperature, sample, 1e-4), atmosphere.TemperatureGradient(sample), 1e-5);
    }

    [Theory]
    [InlineData(1, 12.0, 60.0, 10.0)]
    [InlineData(2, 25.0, 0.0, 0.5)]
    [InlineData(0, 0.0, 0.0, 0.0)]
    public void TheIndexGradientIsTheSlopeOfTheIndex(int layer, double difference, double height, double thickness)
    {
        var atmosphere = new Atmosphere((AtmosphereLayer)layer, difference, height, thickness);

        foreach (var sample in new[] { 0.1, 0.4, 5.0, 55.0, 60.0, 66.0, 300.0 })
        {
            var numeric = (atmosphere.At(sample + 1e-4).Index - atmosphere.At(sample - 1e-4).Index) / 2e-4;
            Assert.Equal(numeric, atmosphere.At(sample).Gradient, 1e-9);
        }
    }

    [Fact]
    public void TheIndexOfStandardAirAtTheGroundMatchesTheRefractivity()
    {
        var (index, _) = Atmosphere.Standard.At(0.0);

        Assert.Equal(1.0 + AirOptics.StandardRefractivity(550.0), index, 1e-15);
    }

    [Fact]
    public void WarmAirBendsLightLessThanColdAir()
    {
        var cold = Atmosphere.Standard.At(0.0).Index;
        var warm = new Atmosphere(AtmosphereLayer.Heated, 30.0, 0.0, 1.0).At(0.0).Index;

        Assert.True(warm < cold);
        Assert.Equal((cold - 1.0) * (288.15 / 318.15), warm - 1.0, 1e-12);
    }

    [Fact]
    public void TheIndexOfStandardAirFallsWithHeight()
    {
        var atmosphere = Atmosphere.Standard;

        Assert.True(atmosphere.At(1000.0).Index < atmosphere.At(0.0).Index);
        Assert.True(atmosphere.At(0.0).Gradient < 0.0);
    }

    [Fact]
    public void AVacuumHasNoRefractionAtAnyHeight()
    {
        foreach (var height in new[] { 0.0, 100.0, 5000.0 })
            Assert.Equal((1.0, 0.0), Atmosphere.Vacuum.At(height));
    }
}
