namespace Mirage;

internal static class MirageHorizon
{
    public static double FocalLength(double width, double halfFieldTangent)
        => 0.5 * width / halfFieldTangent;

    public static double Pitch(double tilt, double position, double focalLength)
        => Math.Clamp(tilt + Math.Atan(position / focalLength), -MirageSettings.MaximumPitchRadians, MirageSettings.MaximumPitchRadians);

    public static double Row(double pitch, double focalLength)
        => focalLength * Math.Tan(pitch);

    public static double Position(double row, double tilt, double focalLength)
        => focalLength * Math.Tan(Math.Atan(row / focalLength) - tilt);
}
