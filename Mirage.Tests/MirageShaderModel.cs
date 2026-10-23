using System.Numerics;

namespace Mirage.Tests;

internal sealed class MirageShaderModel(int width, int height, Func<int, int, Bgra> source, float[] texels, float halfFieldTangent, float pitchCosine, float pitchSine, float amount)
{
    const float MinimumDepth = 1e-4f;
    const float MinimumShift = 1e-9f;
    const int MaximumTaps = 16;
    const float MaximumSine = 0.1f;

    readonly Vector4[] pixels = Premultiplied(width, height, source);

    static Vector4[] Premultiplied(int width, int height, Func<int, int, Bgra> source)
    {
        var values = new Vector4[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = source(x, y).Premultiplied();
                values[y * width + x] = new Vector4(pixel.Blue, pixel.Green, pixel.Red, pixel.Alpha) / 255f;
            }
        }

        return values;
    }

    float ReadShift(float elevation)
    {
        var position = (elevation - (float)MirageSettings.GetMapStartRadians()) / (float)MirageSettings.GetMapSpanRadians();
        if (position < 0f || position > 1f)
            return 0f;

        var x = position * (MirageSettings.MapWidth - 1f);
        var index = Math.Min((int)MathF.Floor(x), MirageSettings.MapWidth - 2);
        var low = texels[index * MirageSettings.TexelChannels];
        var high = texels[(index + 1) * MirageSettings.TexelChannels];
        return low + (high - low) * (x - index);
    }

    Vector4 Sample(Vector2 position)
    {
        var x = Math.Clamp(position.X - 0.5f, 0f, width - 1f);
        var y = Math.Clamp(position.Y - 0.5f, 0f, height - 1f);
        var column = Math.Min((int)MathF.Floor(x), Math.Max(width - 2, 0));
        var row = Math.Min((int)MathF.Floor(y), Math.Max(height - 2, 0));
        var nextColumn = Math.Min(column + 1, width - 1);
        var nextRow = Math.Min(row + 1, height - 1);
        var top = Vector4.Lerp(pixels[row * width + column], pixels[row * width + nextColumn], x - column);
        var bottom = Vector4.Lerp(pixels[nextRow * width + column], pixels[nextRow * width + nextColumn], x - column);
        return Vector4.Lerp(top, bottom, y - row);
    }

    static Bgra ToBgra(Vector4 value)
    {
        byte Channel(float channel) => (byte)Math.Clamp((int)MathF.Round(channel * 255f), 0, 255);
        return new Bgra(Channel(value.X), Channel(value.Y), Channel(value.Z), Channel(value.W));
    }

    public Bgra Shade(int x, int y)
    {
        var original = pixels[y * width + x];
        if (amount <= 0f)
            return ToBgra(original);

        var center = new Vector2(width * 0.5f, height * 0.5f);
        var focal = 0.5f * width / halfFieldTangent;
        var scene = new Vector2(x + 0.5f, y + 0.5f);
        var direction = Vector3.Normalize(new Vector3(scene.X - center.X, scene.Y - center.Y, focal));
        var worldDown = direction.Y * pitchCosine - direction.Z * pitchSine;
        var worldForward = direction.Y * pitchSine + direction.Z * pitchCosine;
        if (MathF.Abs(worldDown) > MaximumSine)
            return ToBgra(original);

        var elevation = MathF.Asin(-worldDown);
        var azimuth = MathF.Atan2(direction.X, worldForward);
        var pixelAngle = 1f / focal;

        var lowShift = ReadShift(elevation - 0.5f * pixelAngle);
        var highShift = ReadShift(elevation + 0.5f * pixelAngle);
        if (MathF.Abs(lowShift) <= MinimumShift && MathF.Abs(highShift) <= MinimumShift && MathF.Abs(ReadShift(elevation)) <= MinimumShift)
            return ToBgra(original);

        var stretch = MathF.Abs(pixelAngle + highShift - lowShift) / pixelAngle;
        var taps = Math.Clamp(MathF.Ceiling(stretch), 1f, MaximumTaps);
        var (sinAzimuth, cosAzimuth) = MathF.SinCos(azimuth);
        var sum = Vector4.Zero;
        for (var tap = 0; tap < MaximumTaps && tap < taps; tap++)
        {
            var sampled = elevation + ((tap + 0.5f) / taps - 0.5f) * pixelAngle;
            var seen = sampled + ReadShift(sampled);
            var (sinSeen, cosSeen) = MathF.SinCos(seen);
            var seenDown = -sinSeen;
            var seenForward = cosAzimuth * cosSeen;
            var seenDirection = new Vector3(sinAzimuth * cosSeen, seenDown * pitchCosine + seenForward * pitchSine, -seenDown * pitchSine + seenForward * pitchCosine);
            var position = center + focal * new Vector2(seenDirection.X, seenDirection.Y) / MathF.Max(seenDirection.Z, MinimumDepth);
            sum += Sample(Vector2.Clamp(position, new Vector2(0.5f, 0.5f), new Vector2(width - 0.5f, height - 0.5f)));
        }

        return ToBgra(Vector4.Lerp(original, sum / taps, amount));
    }
}
