using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Mirage;

internal static class MirageTable
{
    public static double[] BuildStandard(double eyeHeight, double distanceMeters)
    {
        var coordinates = new double[MirageSettings.MapWidth];
        ForEachRay(index =>
        {
            var result = RayTracer.Trace(Atmosphere.Standard, eyeHeight, MirageSettings.GetMapAngleRadians(index), distanceMeters, double.PositiveInfinity);
            coordinates[index] = Coordinate(result, distanceMeters);
        });
        return coordinates;
    }

    public static float[] BuildSuperior(double eyeHeight, double difference, double height, double thickness, double distanceMeters, MirageSource source, double[] standard)
        => BuildSuperior(eyeHeight, difference, height, thickness, distanceMeters, source, standard, out _);

    public static float[] BuildSuperior(double eyeHeight, double difference, double height, double thickness, double distanceMeters, MirageSource source, double[] standard, out float[] fill)
    {
        var atmosphere = new Atmosphere(AtmosphereLayer.Inversion, difference, height, thickness);
        var shifts = new float[MirageSettings.MapWidth];
        var unseen = new float[MirageSettings.MapWidth];
        var (gapLow, gapHigh) = HiddenCoordinates(standard);
        ForEachRay(index =>
        {
            var angle = MirageSettings.GetMapAngleRadians(index);
            var result = RayTracer.Trace(atmosphere, eyeHeight, angle, distanceMeters, double.PositiveInfinity);
            var coordinate = Coordinate(result, distanceMeters);
            var seen = source == MirageSource.Photo ? StandardElevation(standard, coordinate) : GeometricElevation(coordinate, eyeHeight, distanceMeters);
            shifts[index] = (float)(seen - angle);
            if (source == MirageSource.Photo && coordinate > gapLow && coordinate < gapHigh)
                unseen[index] = 1f;
        });
        fill = unseen;
        return shifts;
    }

    private static void ForEachRay(Action<int> trace)
        => Parallel.ForEach(Partitioner.Create(0, MirageSettings.MapWidth, 1), range => trace(range.Item1));

    public static (double Low, double High) HiddenCoordinates(double[] standard)
    {
        var first = Array.FindIndex(standard, coordinate => coordinate >= 0.0);
        return first > 0 ? (standard[first - 1], standard[first]) : (double.NaN, double.NaN);
    }

    public static float[] BuildInferior(double eyeHeight, double difference, double thickness)
    {
        var heated = new Atmosphere(AtmosphereLayer.Heated, difference, 0.0, thickness);
        var unheated = new Atmosphere(AtmosphereLayer.Heated, 0.0, 0.0, thickness);
        var ceiling = MirageSettings.HeatedCeilingFactor * thickness;
        var width = MirageSettings.MapWidth;
        var reference = new double[width];
        var observed = new double[width];
        ForEachRay(index =>
        {
            var angle = MirageSettings.GetMapAngleRadians(index);
            reference[index] = Direction(RayTracer.Trace(unheated, eyeHeight, angle, double.PositiveInfinity, ceiling));
            observed[index] = Direction(RayTracer.Trace(heated, eyeHeight, angle, double.PositiveInfinity, ceiling));
        });

        var directions = new List<double>(width);
        var angles = new List<double>(width);
        for (var index = 0; index < width; index++)
        {
            if (double.IsNaN(reference[index]))
                continue;

            directions.Add(reference[index]);
            angles.Add(MirageSettings.GetMapAngleRadians(index));
        }

        var shifts = new float[width];
        if (directions.Count < 2)
            return shifts;

        for (var index = 0; index < width; index++)
        {
            if (double.IsNaN(observed[index]))
                continue;

            shifts[index] = (float)(Interpolate(directions, angles, observed[index]) - MirageSettings.GetMapAngleRadians(index));
        }

        return shifts;
    }

    private static double Direction(RayResult result)
        => result.End == RayEnd.Exited ? result.Elevation - result.Distance / MirageSettings.EarthRadiusMeters : double.NaN;

    private static double Interpolate(List<double> keys, List<double> values, double key)
    {
        var last = keys.Count - 1;
        if (key <= keys[0])
            return values[0];
        if (key >= keys[last])
            return values[last];

        var low = 0;
        var high = last;
        while (high - low > 1)
        {
            var middle = (low + high) / 2;
            if (keys[middle] <= key)
                low = middle;
            else
                high = middle;
        }

        var fraction = (key - keys[low]) / (keys[high] - keys[low]);
        return values[low] + fraction * (values[high] - values[low]);
    }

    public static float[] ToTexels(float[] shifts, float[]? fill = null)
    {
        var texels = new float[shifts.Length * MirageSettings.TexelChannels];
        for (var index = 0; index < shifts.Length; index++)
        {
            texels[index * MirageSettings.TexelChannels] = shifts[index];
            if (fill is not null)
                texels[index * MirageSettings.TexelChannels + 1] = fill[index];
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
