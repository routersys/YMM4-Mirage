using System.Runtime.CompilerServices;

namespace Mirage;

internal static class MirageTable
{
    public static double[] BuildStandard(double eyeHeight, double distanceMeters)
    {
        var coordinates = new double[MirageSettings.MapWidth];
        Parallel.For(0, MirageSettings.MapWidth, index =>
        {
            var result = RayTracer.Trace(Atmosphere.Standard, eyeHeight, MirageSettings.GetMapAngleRadians(index), distanceMeters, double.PositiveInfinity);
            coordinates[index] = Coordinate(result, distanceMeters);
        });
        return coordinates;
    }

    public static float[] BuildSuperior(double eyeHeight, double difference, double height, double thickness, double distanceMeters, MirageSource source, double[] standard)
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Inversion, difference, height, thickness);
        var shifts = new float[MirageSettings.MapWidth];
        Parallel.For(0, MirageSettings.MapWidth, index =>
        {
            var angle = MirageSettings.GetMapAngleRadians(index);
            var result = RayTracer.Trace(atmosphere, eyeHeight, angle, distanceMeters, double.PositiveInfinity);
            var coordinate = Coordinate(result, distanceMeters);
            var seen = source == MirageSource.Photo ? StandardElevation(standard, coordinate) : GeometricElevation(coordinate, eyeHeight, distanceMeters);
            shifts[index] = (float)(seen - angle);
        });
        return shifts;
    }

    public static float[] BuildInferior(double eyeHeight, double difference, double thickness)
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Heated, difference, 0.0, thickness);
        var ceiling = MirageSettings.HeatedCeilingFactor * thickness;
        var shifts = new float[MirageSettings.MapWidth];
        Parallel.For(0, MirageSettings.MapWidth, index =>
        {
            var angle = MirageSettings.GetMapAngleRadians(index);
            var result = RayTracer.Trace(atmosphere, eyeHeight, angle, double.PositiveInfinity, ceiling);
            shifts[index] = result.End is RayEnd.Hit or RayEnd.Passed ? 0f : (float)(result.Elevation - angle);
        });
        return shifts;
    }

    public static float[] ToTexels(float[] shifts)
    {
        var texels = new float[shifts.Length * MirageSettings.TexelChannels];
        for (var index = 0; index < shifts.Length; index++)
        {
            texels[index * MirageSettings.TexelChannels] = shifts[index];
            texels[index * MirageSettings.TexelChannels + 3] = 1f;
        }

        return texels;
    }

    public static double Coordinate(RayResult result, double distanceMeters)
        => result.End == RayEnd.Hit ? -(distanceMeters - result.Distance) : result.Height;

    public static double StandardElevation(double[] standard, double coordinate)
    {
        var last = standard.Length - 1;
        if (coordinate <= standard[0])
            return MirageSettings.GetMapAngleRadians(0);
        if (coordinate >= standard[last])
            return MirageSettings.GetMapAngleRadians(last);

        var low = 0;
        var high = last;
        while (high - low > 1)
        {
            var middle = (low + high) / 2;
            if (standard[middle] <= coordinate)
                low = middle;
            else
                high = middle;
        }

        var fraction = (coordinate - standard[low]) / (standard[high] - standard[low]);
        return MirageSettings.GetMapAngleRadians(low) + fraction * (MirageSettings.GetMapAngleRadians(high) - MirageSettings.GetMapAngleRadians(low));
    }

    public static double GeometricElevation(double coordinate, double eyeHeight, double distanceMeters)
    {
        var earth = MirageSettings.EarthRadiusMeters;
        var height = Math.Max(coordinate, 0.0);
        var arc = (coordinate >= 0.0 ? distanceMeters : distanceMeters + coordinate) / earth;
        return Math.Atan2((earth + height) * Math.Cos(arc) - (earth + eyeHeight), (earth + height) * Math.Sin(arc));
    }
}
