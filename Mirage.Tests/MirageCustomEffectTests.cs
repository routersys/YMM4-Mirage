using YukkuriMovieMaker.Commons;
using static Mirage.MirageCustomEffect;

namespace Mirage.Tests;

[Collection("Direct2D")]
public sealed class MirageCustomEffectTests
{
    const int Width = 64;
    const int Height = 96;
    const int Tolerance = 3;

    static float Read(MirageCustomEffect effect, PropertyIndex index) => effect.GetFloatValue((int)index);

    static Bgra Gradient(int x, int y) => Bgra.Opaque((byte)(y * 2), (byte)(x * 3), (byte)(255 - y * 2));

    static Rendering Render(IGraphicsDevicesAndContext devices, SourceImage source, MapImage map, Action<MirageCustomEffect> configure)
    {
        using var effect = new MirageCustomEffect(devices);
        effect.SetInput(0, source.Bitmap, true);
        effect.SetInput(1, map.Bitmap, true);
        configure(effect);
        using var output = effect.Output;

        return Rendering.Capture(devices, output);
    }

    static void AssertFollowsTheModel(Rendering rendering, MirageShaderModel model, string where)
    {
        Assert.All(rendering.Coordinates(), point =>
        {
            var expected = model.Shade(point.X, point.Y);
            var actual = rendering[point.X, point.Y];
            Assert.True(Math.Abs(expected.Blue - actual.Blue) <= Tolerance, $"{where} {point} blue {expected} {actual}");
            Assert.True(Math.Abs(expected.Green - actual.Green) <= Tolerance, $"{where} {point} green {expected} {actual}");
            Assert.True(Math.Abs(expected.Red - actual.Red) <= Tolerance, $"{where} {point} red {expected} {actual}");
            Assert.True(Math.Abs(expected.Alpha - actual.Alpha) <= Tolerance, $"{where} {point} alpha {expected} {actual}");
        });
    }

    [Fact]
    public void TheEffectIsEnabledOnceCreated()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new MirageCustomEffect(context);

        Assert.True(effect.IsEnabled);
    }

    [Fact]
    public void ThePropertiesStartFromTheirDefaults()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new MirageCustomEffect(context);

        Assert.Equal(0f, Read(effect, PropertyIndex.Amount));
        Assert.Equal(1f, Read(effect, PropertyIndex.HalfFieldTangent));
        Assert.Equal(1f, Read(effect, PropertyIndex.PitchCosine));
        Assert.Equal(0f, Read(effect, PropertyIndex.PitchSine));
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(1f, 1f)]
    [InlineData(1.5f, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(float.NegativeInfinity, 0f)]
    [InlineData(float.NaN, 0f)]
    public void TheAmountIsClampedToTheUnitRange(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new MirageCustomEffect(context);

        effect.Amount = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.Amount));
    }

    [Theory]
    [InlineData(-2f, -1f)]
    [InlineData(-0.5f, -0.5f)]
    [InlineData(0f, 0f)]
    [InlineData(0.75f, 0.75f)]
    [InlineData(3f, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(float.NegativeInfinity, -1f)]
    [InlineData(float.NaN, 0f)]
    public void ThePitchStaysInsideTheUnitRange(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new MirageCustomEffect(context);

        effect.PitchCosine = value;
        effect.PitchSine = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.PitchCosine));
        Assert.Equal(expected, Read(effect, PropertyIndex.PitchSine));
    }

    [Theory]
    [InlineData(0.01f)]
    [InlineData(0.05f)]
    [InlineData(0.3f)]
    public void TheHalfFieldTangentKeepsValuesInsideTheFieldOfViewLimits(float value)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new MirageCustomEffect(context);

        effect.HalfFieldTangent = value;

        Assert.Equal(value, Read(effect, PropertyIndex.HalfFieldTangent));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(0f)]
    [InlineData(float.NegativeInfinity)]
    public void TheHalfFieldTangentNeverFallsBelowTheNarrowestFieldOfView(float value)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new MirageCustomEffect(context);

        effect.HalfFieldTangent = value;

        Assert.Equal(MirageSettings.MinimumHalfFieldTangent, Read(effect, PropertyIndex.HalfFieldTangent));
    }

    [Theory]
    [InlineData(4f)]
    [InlineData(1e5f)]
    [InlineData(float.PositiveInfinity)]
    public void TheHalfFieldTangentNeverExceedsTheWidestFieldOfView(float value)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new MirageCustomEffect(context);

        effect.HalfFieldTangent = value;

        Assert.Equal(MirageSettings.MaximumHalfFieldTangent, Read(effect, PropertyIndex.HalfFieldTangent));
    }

    [Fact]
    public void ANonNumberLeavesTheHalfFieldTangentAtItsDefault()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new MirageCustomEffect(context);

        effect.HalfFieldTangent = float.NaN;

        Assert.Equal(1f, Read(effect, PropertyIndex.HalfFieldTangent));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.6f)]
    [InlineData(1f)]
    public void TheOutputBoundsEqualTheInput(float amount)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 40, 24, Bgra.Opaque(10, 20, 30));
        using var map = MapImage.Constant(context, 0.001f);
        using var effect = new MirageCustomEffect(context);
        effect.SetInput(0, source.Bitmap, true);
        effect.SetInput(1, map.Bitmap, true);
        effect.Amount = amount;
        using var output = effect.Output;

        var bounds = context.DeviceContext.GetImageLocalBounds(output);

        Assert.Equal((0f, 0f, 40f, 24f), (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
    }

    [Fact]
    public void WithoutAnAmountTheInputPassesThroughUnchanged()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, (x, y) => new Bgra((byte)(x * 5), (byte)(y * 2), (byte)(x + y), (byte)(100 + x)));
        using var map = MapImage.Constant(context, 0.001f);

        var rendered = Render(context, source, map, effect =>
        {
            effect.Amount = 0f;
            effect.HalfFieldTangent = MirageSettings.MinimumHalfFieldTangent;
        });

        Assert.True(rendered.SamePixelsAs(Rendering.Capture(context, source.Bitmap)));
    }

    [Fact]
    public void AMapWithoutShiftPassesTheInputThroughUnchanged()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var map = MapImage.Constant(context, 0f);

        var rendered = Render(context, source, map, effect =>
        {
            effect.Amount = 1f;
            effect.HalfFieldTangent = MirageSettings.MinimumHalfFieldTangent;
        });

        Assert.True(rendered.SamePixelsAs(Rendering.Capture(context, source.Bitmap)));
    }

    [Fact]
    public void DirectionsOutsideTheMapPassThroughUnchanged()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var map = MapImage.Constant(context, 0.001f);
        var pitch = 20.0 * MirageSettings.RadiansPerDegree;

        var rendered = Render(context, source, map, effect =>
        {
            effect.Amount = 1f;
            effect.HalfFieldTangent = MirageSettings.MinimumHalfFieldTangent;
            effect.PitchCosine = (float)Math.Cos(pitch);
            effect.PitchSine = (float)Math.Sin(pitch);
        });

        Assert.True(rendered.SamePixelsAs(Rendering.Capture(context, source.Bitmap)));
    }

    [Fact]
    public void ARaisedSeenDirectionMovesTheImageDownByTheFocalLengthTimesTheShift()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        const float Shift = 2e-4f;
        using var source = new SourceImage(context, Width, Height, (_, y) => Bgra.Opaque((byte)(y * 2), 0, 0));
        using var map = MapImage.Constant(context, Shift);

        var rendered = Render(context, source, map, effect =>
        {
            effect.Amount = 1f;
            effect.HalfFieldTangent = MirageSettings.MinimumHalfFieldTangent;
        });

        var focal = 0.5 * Width / MirageSettings.MinimumHalfFieldTangent;
        Assert.All(Enumerable.Range(30, 36), y =>
        {
            var up = (Height * 0.5 - (y + 0.5)) / focal;
            var position = Height * 0.5 - focal * Math.Tan(Math.Atan(up) + Shift);
            Assert.True(Math.Abs(rendered[Width / 2, y].Blue - 2.0 * (position - 0.5)) <= 1.0, $"row {y}");
        });
    }

    [Fact]
    public void ADecreasingShiftMovesTheImageUp()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, (_, y) => Bgra.Opaque((byte)(y * 2), 0, 0));
        using var up = MapImage.Constant(context, 2e-4f);
        using var down = MapImage.Constant(context, -2e-4f);

        void Configure(MirageCustomEffect effect)
        {
            effect.Amount = 1f;
            effect.HalfFieldTangent = MirageSettings.MinimumHalfFieldTangent;
        }

        var raised = Render(context, source, up, Configure);
        var lowered = Render(context, source, down, Configure);

        Assert.True(raised[Width / 2, Height / 2].Blue < lowered[Width / 2, Height / 2].Blue);
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(0f, 0.5f)]
    [InlineData(2e-4f, 1f)]
    [InlineData(-3e-4f, 0.6f)]
    public void ConstantShiftsFollowTheModelOfTheShader(float shift, float amount)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var map = MapImage.Constant(context, shift);

        var rendered = Render(context, source, map, effect =>
        {
            effect.Amount = amount;
            effect.HalfFieldTangent = MirageSettings.MinimumHalfFieldTangent;
        });

        var model = new MirageShaderModel(Width, Height, Gradient, MirageTable.ToTexels(Enumerable.Repeat(shift, MirageSettings.MapWidth).ToArray()), MirageSettings.MinimumHalfFieldTangent, 1f, 0f, amount);
        AssertFollowsTheModel(rendered, model, $"shift {shift}");
    }

    [Theory]
    [InlineData(2.5f)]
    [InlineData(-0.5f)]
    [InlineData(30f)]
    public void ASlopedShiftAveragesTheSamplesOfEveryPixelItStretches(float slope)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, (x, y) => Bgra.Opaque((byte)(y % 2 * 200), (byte)(x * 3), 90));
        using var map = MapImage.Sloped(context, slope);

        var rendered = Render(context, source, map, effect =>
        {
            effect.Amount = 1f;
            effect.HalfFieldTangent = MirageSettings.MinimumHalfFieldTangent;
        });

        var texels = MirageTable.ToTexels(Enumerable.Range(0, MirageSettings.MapWidth).Select(index => (float)(slope * MirageSettings.GetMapAngleRadians(index))).ToArray());
        var model = new MirageShaderModel(Width, Height, (x, y) => Bgra.Opaque((byte)(y % 2 * 200), (byte)(x * 3), 90), texels, MirageSettings.MinimumHalfFieldTangent, 1f, 0f, 1f);
        AssertFollowsTheModel(rendered, model, $"slope {slope}");
    }

    [Fact]
    public void ThePitchTurnsTheViewBeforeTheMapIsRead()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var map = MapImage.Sloped(context, 5f);
        var pitch = -0.04 * MirageSettings.RadiansPerDegree;

        var rendered = Render(context, source, map, effect =>
        {
            effect.Amount = 1f;
            effect.HalfFieldTangent = MirageSettings.MinimumHalfFieldTangent;
            effect.PitchCosine = (float)Math.Cos(pitch);
            effect.PitchSine = (float)Math.Sin(pitch);
        });

        var texels = MirageTable.ToTexels(Enumerable.Range(0, MirageSettings.MapWidth).Select(index => (float)(5f * MirageSettings.GetMapAngleRadians(index))).ToArray());
        var model = new MirageShaderModel(Width, Height, Gradient, texels, MirageSettings.MinimumHalfFieldTangent, (float)Math.Cos(pitch), (float)Math.Sin(pitch), 1f);
        AssertFollowsTheModel(rendered, model, "pitch");
    }

    [Fact]
    public void AFilledMapAveragesTheNeighborsAlongTheRow()
    {
        const int WideWidth = 800;
        const int WideHeight = 40;
        static Bgra Stripes(int x, int y) => Bgra.Opaque((byte)(x % 4 < 2 ? 0 : 240), 40, 90);
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, WideWidth, WideHeight, Stripes);
        using var plain = MapImage.Constant(context, 2e-4f);
        using var filled = MapImage.Filled(context, 2e-4f);

        void Configure(MirageCustomEffect effect)
        {
            effect.Amount = 1f;
            effect.HalfFieldTangent = MirageSettings.MinimumHalfFieldTangent;
        }

        var sharp = Render(context, source, plain, Configure);
        var smooth = Render(context, source, filled, Configure);

        var texels = MirageTable.ToTexels(Enumerable.Repeat(2e-4f, MirageSettings.MapWidth).ToArray(), Enumerable.Repeat(1f, MirageSettings.MapWidth).ToArray());
        AssertFollowsTheModel(smooth, new MirageShaderModel(WideWidth, WideHeight, Stripes, texels, MirageSettings.MinimumHalfFieldTangent, 1f, 0f, 1f), "filled");
        var row = WideHeight / 2;
        var sharpRange = Enumerable.Range(100, 400).Select(x => (int)sharp[x, row].Blue).ToArray();
        var smoothRange = Enumerable.Range(100, 400).Select(x => (int)smooth[x, row].Blue).ToArray();
        Assert.True(sharpRange.Max() - sharpRange.Min() > 200);
        Assert.True(smoothRange.Max() - smoothRange.Min() < 90);
    }

    [Fact]
    public void AMapWithoutFillLeavesTheRowSharp()
    {
        const int WideWidth = 800;
        static Bgra Stripes(int x, int y) => Bgra.Opaque((byte)(x % 4 < 2 ? 0 : 240), 40, 90);
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, WideWidth, 40, Stripes);
        using var map = MapImage.Constant(context, 2e-4f);

        var rendered = Render(context, source, map, effect =>
        {
            effect.Amount = 1f;
            effect.HalfFieldTangent = MirageSettings.MinimumHalfFieldTangent;
        });

        var texels = MirageTable.ToTexels(Enumerable.Repeat(2e-4f, MirageSettings.MapWidth).ToArray());
        AssertFollowsTheModel(rendered, new MirageShaderModel(WideWidth, 40, Stripes, texels, MirageSettings.MinimumHalfFieldTangent, 1f, 0f, 1f), "unfilled");
    }

    [Fact]
    public void TransparentColumnsStayTransparent()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, (x, y) => x < Width / 2 ? Bgra.Transparent : Gradient(x, y));
        using var map = MapImage.Constant(context, 2e-4f);

        var rendered = Render(context, source, map, effect =>
        {
            effect.Amount = 1f;
            effect.HalfFieldTangent = MirageSettings.MinimumHalfFieldTangent;
        });

        var untouched = Rendering.Capture(context, source.Bitmap);
        Assert.All(rendered.Coordinates().Where(point => point.X < Width / 2), point => Assert.Equal(Bgra.Transparent, rendered[point.X, point.Y]));
        Assert.Contains(rendered.Coordinates().Where(point => point.X >= Width / 2), point => rendered[point.X, point.Y] != untouched[point.X, point.Y]);
    }
}
