using System.Runtime.CompilerServices;

namespace Mirage;

internal enum RayEnd
{
    Reached,
    Hit,
    Exited,
    Passed,
}

internal readonly record struct RayResult(RayEnd End, double Height, double Distance, double Elevation);

internal static class RayTracer
{
    private readonly record struct State(double Elevation, double Radius, double Arc);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static RayResult Trace(Atmosphere atmosphere, double eyeHeight, double elevation, double targetDistance, double ceilingHeight)
    {
        var earth = MirageSettings.EarthRadiusMeters;
        var scale = atmosphere.Layer == AtmosphereLayer.None ? MirageSettings.StandardLayerScaleMeters : atmosphere.Thickness;
        var state = new State(elevation, earth + eyeHeight, 0.0);
        if (eyeHeight >= ceilingHeight && Math.Sin(elevation) >= 0.0)
            return new RayResult(RayEnd.Exited, eyeHeight, 0.0, elevation);

        var invariant = atmosphere.At(eyeHeight).Index * state.Radius * Math.Cos(elevation);

        for (var step = 0; step < MirageSettings.MaximumSteps; step++)
        {
            var slope = Math.Max(Math.Abs(Math.Sin(state.Elevation)), MirageSettings.MinimumSlope);
            var length = Math.Clamp(scale / (MirageSettings.StepsPerLayerThickness * slope), MirageSettings.MinimumStepMeters, MirageSettings.MaximumStepMeters);
            var remaining = (targetDistance / earth - state.Arc) * state.Radius / Math.Max(Math.Cos(state.Elevation), MirageSettings.MinimumSlope);
            var last = remaining <= length;
            if (last)
                length = Math.Max(remaining, 0.0);

            var next = Advance(atmosphere, state, length);
            var height = next.Radius - earth;
            if (height <= 0.0)
            {
                var previous = state.Radius - earth;
                var fraction = previous / (previous - height);
                return new RayResult(RayEnd.Hit, 0.0, earth * (state.Arc + fraction * (next.Arc - state.Arc)), state.Elevation + fraction * (next.Elevation - state.Elevation));
            }

            if (state.Elevation < 0.0 && next.Elevation >= 0.0)
            {
                var fraction = -state.Elevation / (next.Elevation - state.Elevation);
                var lowest = state.Radius - earth + 0.5 * state.Elevation * fraction * length;
                if (lowest <= 0.0)
                    return new RayResult(RayEnd.Hit, 0.0, earth * (state.Arc + fraction * (next.Arc - state.Arc)), 0.0);
            }

            if (state.Radius - earth < ceilingHeight && height >= ceilingHeight)
            {
                var previous = state.Radius - earth;
                var fraction = (ceilingHeight - previous) / (height - previous);
                var cosine = Math.Min(1.0, invariant / (atmosphere.At(ceilingHeight).Index * (earth + ceilingHeight)));
                return new RayResult(RayEnd.Exited, ceilingHeight, earth * (state.Arc + fraction * (next.Arc - state.Arc)), Math.Acos(cosine));
            }

            if (last)
                return new RayResult(RayEnd.Reached, height, earth * next.Arc, next.Elevation);

            if (earth * next.Arc >= MirageSettings.MaximumRayDistanceMeters && double.IsPositiveInfinity(targetDistance))
                return new RayResult(RayEnd.Passed, height, earth * next.Arc, next.Elevation);

            state = next;
        }

        return new RayResult(RayEnd.Passed, state.Radius - earth, earth * state.Arc, state.Elevation);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static State Advance(Atmosphere atmosphere, State state, double length)
    {
        var (k1e, k1r, k1p) = Derivative(atmosphere, state);
        var (k2e, k2r, k2p) = Derivative(atmosphere, Offset(state, 0.5 * length, k1e, k1r, k1p));
        var (k3e, k3r, k3p) = Derivative(atmosphere, Offset(state, 0.5 * length, k2e, k2r, k2p));
        var (k4e, k4r, k4p) = Derivative(atmosphere, Offset(state, length, k3e, k3r, k3p));
        return new State(
            state.Elevation + length / 6.0 * (k1e + 2.0 * k2e + 2.0 * k3e + k4e),
            state.Radius + length / 6.0 * (k1r + 2.0 * k2r + 2.0 * k3r + k4r),
            state.Arc + length / 6.0 * (k1p + 2.0 * k2p + 2.0 * k3p + k4p));
    }

    private static State Offset(State state, double length, double elevation, double radius, double arc)
        => new(state.Elevation + length * elevation, state.Radius + length * radius, state.Arc + length * arc);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (double Elevation, double Radius, double Arc) Derivative(Atmosphere atmosphere, State state)
    {
        var (index, gradient) = atmosphere.At(state.Radius - MirageSettings.EarthRadiusMeters);
        var cosine = Math.Cos(state.Elevation);
        return (cosine * (1.0 / state.Radius + gradient / index), Math.Sin(state.Elevation), cosine / state.Radius);
    }
}
