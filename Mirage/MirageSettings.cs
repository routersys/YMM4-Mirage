namespace Mirage;

internal static class MirageSettings
{
    public const int TexelChannels = 4;
    public const int TexelBytes = TexelChannels * sizeof(float);
    public const double RadiansPerDegree = Math.PI / 180.0;

    public const int MapWidth = 4096;
    public const double MapStartDegrees = -2.0;
    public const double MapEndDegrees = 2.0;

    public const double EarthRadiusMeters = 6371000.0;
    public const double StandardTemperature = 288.15;
    public const double StandardPressure = 101325.0;
    public const double LapseRate = 0.0065;
    public const double BarometricExponent = 5.25588;
    public const double WavelengthNanometers = 550.0;

    public const double MinimumEyeHeightMeters = 0.5;
    public const double MaximumEyeHeightMeters = 20.0;
    public const double MinimumHeatDifference = 5.0;
    public const double MaximumHeatDifference = 60.0;
    public const double MinimumHeatThicknessMeters = 0.1;
    public const double MaximumHeatThicknessMeters = 3.0;
    public const double MinimumInversionDifference = 1.0;
    public const double MaximumInversionDifference = 20.0;
    public const double MinimumInversionHeightMeters = 10.0;
    public const double MaximumInversionHeightMeters = 300.0;
    public const double MinimumInversionThicknessMeters = 2.0;
    public const double MaximumInversionThicknessMeters = 50.0;
    public const double MinimumDistanceKilometers = 5.0;
    public const double MaximumDistanceKilometers = 100.0;
    public const double MinimumFieldOfViewDegrees = 0.2;
    public const double MaximumFieldOfViewDegrees = 60.0;
    public const double MinimumCameraPitchDegrees = -20.0;
    public const double MaximumCameraPitchDegrees = 20.0;

    public const double MetersPerKilometer = 1000.0;
    public const double StandardLayerScaleMeters = 100.0;
    public const double HeatedCeilingFactor = 10.0;
    public const double MaximumRayDistanceMeters = 50000.0;
    public const double StepsPerLayerThickness = 10.0;
    public const double MinimumStepMeters = 0.5;
    public const double MaximumStepMeters = 200.0;
    public const double MinimumSlope = 1e-4;
    public const int MaximumSteps = 2_000_000;
    public const int MaximumTaps = 16;

    public static readonly float MinimumHalfFieldTangent = GetHalfFieldOfViewTangent(MinimumFieldOfViewDegrees);
    public static readonly float MaximumHalfFieldTangent = GetHalfFieldOfViewTangent(MaximumFieldOfViewDegrees);

    public static double GetMapStartRadians()
        => MapStartDegrees * RadiansPerDegree;

    public static double GetMapSpanRadians()
        => (MapEndDegrees - MapStartDegrees) * RadiansPerDegree;

    public static double GetMapAngleRadians(int index)
        => GetMapStartRadians() + index * GetMapSpanRadians() / (MapWidth - 1);

    public static float GetHalfFieldOfViewTangent(double fieldOfViewDegrees)
    {
        var field = Math.Clamp(fieldOfViewDegrees, MinimumFieldOfViewDegrees, MaximumFieldOfViewDegrees);
        return (float)Math.Tan(field * RadiansPerDegree / 2.0);
    }
}
