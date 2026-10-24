using System.Runtime.CompilerServices;

namespace Mirage;

internal enum AtmosphereLayer
{
    None,
    Inversion,
    Heated,
}

internal readonly record struct Atmosphere(AtmosphereLayer Layer, double Difference, double Height, double Thickness, bool Refracting = true)
{
    private static readonly double Refractivity = AirOptics.StandardRefractivity(MirageSettings.WavelengthNanometers);

    public static Atmosphere Standard => new(AtmosphereLayer.None, 0.0, 0.0, 0.0);

    public static Atmosphere Vacuum => new(AtmosphereLayer.None, 0.0, 0.0, 0.0, false);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public double Temperature(double height)
        => MirageSettings.StandardTemperature - MirageSettings.LapseRate * height + LayerValues(height).Temperature;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public double TemperatureGradient(double height)
        => -MirageSettings.LapseRate + LayerValues(height).Gradient;

    public double Pressure(double height)
        => MirageSettings.StandardPressure * Math.Pow(PressureRatio(height), MirageSettings.BarometricExponent);

    public double PressureGradient(double height)
        => -MirageSettings.StandardPressure * MirageSettings.BarometricExponent * MirageSettings.LapseRate / MirageSettings.StandardTemperature
            * Math.Pow(PressureRatio(height), MirageSettings.BarometricExponent - 1.0);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public (double Index, double Gradient) At(double height)
    {
        if (!Refracting)
            return (1.0, 0.0);

        var ratio = PressureRatio(height);
        var power = Math.Pow(ratio, MirageSettings.BarometricExponent - 1.0);
        var pressure = MirageSettings.StandardPressure * power * ratio;
        var pressureGradient = -MirageSettings.StandardPressure * MirageSettings.BarometricExponent * MirageSettings.LapseRate / MirageSettings.StandardTemperature * power;
        var layer = LayerValues(height);
        var temperature = MirageSettings.StandardTemperature - MirageSettings.LapseRate * height + layer.Temperature;
        var index = 1.0 + Refractivity * (pressure / MirageSettings.StandardPressure) * (MirageSettings.StandardTemperature / temperature);
        var gradient = Refractivity * MirageSettings.StandardTemperature / MirageSettings.StandardPressure
            * (pressureGradient / temperature - pressure * (-MirageSettings.LapseRate + layer.Gradient) / (temperature * temperature));
        return (index, gradient);
    }

    private static double PressureRatio(double height)
        => 1.0 - MirageSettings.LapseRate * height / MirageSettings.StandardTemperature;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private (double Temperature, double Gradient) LayerValues(double height)
    {
        switch (Layer)
        {
            case AtmosphereLayer.Inversion:
                var scaled = (height - Height) / Thickness;
                var cosine = Math.Cosh(scaled);
                return (Difference * 0.5 * (1.0 + Math.Tanh(scaled)), Difference * 0.5 / (Thickness * cosine * cosine));
            case AtmosphereLayer.Heated:
                var decay = Math.Exp(-height / Thickness);
                return (Difference * decay, -Difference / Thickness * decay);
            default:
                return (0.0, 0.0);
        }
    }
}

internal static class AirOptics
{
    private const double NanometersPerMicrometer = 1000.0;

    public static double StandardRefractivity(double wavelengthNanometers)
    {
        var wavenumber = NanometersPerMicrometer / wavelengthNanometers;
        var squared = wavenumber * wavenumber;
        return (5792105.0 / (238.0185 - squared) + 167917.0 / (57.362 - squared)) * 1e-8;
    }

    public static double Refractivity(double wavelengthNanometers, double temperature, double pressure)
        => StandardRefractivity(wavelengthNanometers) * (pressure / MirageSettings.StandardPressure) * (MirageSettings.StandardTemperature / temperature);
}
