using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Player.Video;

namespace Mirage.Tests;

[Collection("Direct2D")]
public sealed class MirageEffectProcessorTests
{
    const int Width = 128;
    const int Height = 96;
    const int Length = 30;
    const int Tolerance = 3;

    static Bgra Pattern(int x, int y) => Bgra.Opaque((byte)(y * 2), (byte)(x * 2), (byte)(255 - y * 2));

    static Bgra HalfTransparent(int x, int y)
        => x < Width / 2 ? Pattern(x, y) : Pattern(x, y) with { Alpha = 128 };

    static Bgra TransparentColumns(int x, int y) => x < Width / 2 ? Bgra.Transparent : Pattern(x, y);

    static MirageEffect Inferior(double amount = 100d)
    {
        var effect = new MirageEffect();
        effect.Amount.Values[0].Value = amount;
        return effect;
    }

    static MirageEffect Superior(double amount = 100d)
    {
        var effect = new MirageEffect { Kind = MirageKind.Superior };
        effect.Amount.Values[0].Value = amount;
        return effect;
    }

    static Animation Linear(double from, double to)
        => Json.LoadFromText<Animation>(string.Create(CultureInfo.InvariantCulture, $$"""{"AnimationType":"直線移動","Values":[{"Value":{{from}}},{"Value":{{to}}}]}"""))!;

    static Rendering RenderFrame(IGraphicsDevicesAndContext devices, IVideoEffectProcessor processor, int frame)
    {
        processor.Update(EffectDescriptions.At(frame, Length));
        return Rendering.Capture(devices, processor.Output);
    }

    sealed class Model
    {
        readonly Func<int, int, Bgra> source;
        readonly MirageShaderModel shader;

        public Model(MirageEffect effect, int frame, Func<int, int, Bgra> source, int width = Width, int height = Height)
        {
            this.source = source;
            double Value(Animation animation) => animation.GetValue(frame, Length, EffectDescriptions.Fps);
            var eyeHeight = Math.Clamp(Value(effect.EyeHeight), MirageSettings.MinimumEyeHeightMeters, MirageSettings.MaximumEyeHeightMeters);
            float[] shifts;
            float[]? fill = null;
            if (effect.Kind == MirageKind.Inferior)
            {
                shifts = MirageTable.BuildInferior(eyeHeight, Value(effect.HeatDifference), Value(effect.HeatThickness));
            }
            else
            {
                var distance = Value(effect.Distance) * MirageSettings.MetersPerKilometer;
                shifts = MirageTable.BuildSuperior(eyeHeight, Value(effect.InversionDifference), Value(effect.InversionHeight), Value(effect.InversionThickness), distance, effect.Source, MirageTable.BuildStandard(eyeHeight, distance), out var unseen);
                fill = unseen;
            }

            var halfTangent = MirageSettings.GetHalfFieldOfViewTangent(Value(effect.FieldOfView));
            var pitch = MirageHorizon.Pitch(Value(effect.CameraPitch) * MirageSettings.RadiansPerDegree, Value(effect.HorizonPosition), MirageHorizon.FocalLength(width, halfTangent));
            var amplitude = (float)(Math.Clamp(Value(effect.Shimmer), 0d, 100d) / 100.0 * MirageSettings.ShimmerAmplitudeRadians);
            var cycles = Value(effect.ShimmerSpeed) * frame / EffectDescriptions.Fps;
            var phases = Enumerable.Range(0, 4).Select(component => amplitude > 0f ? MirageSettings.GetShimmerPhase(component, cycles) : 0f).ToArray();
            shader = new MirageShaderModel(width, height, source, MirageTable.ToTexels(shifts, fill), halfTangent, (float)Math.Cos(pitch), (float)Math.Sin(pitch), (float)(Value(effect.Amount) / 100.0), amplitude, phases);
        }

        public Bgra Expected(int x, int y) => shader.Shade(x, y);
    }

    static void AssertClose(Bgra expected, Bgra actual, string where)
    {
        Assert.True(Math.Abs(expected.Blue - actual.Blue) <= Tolerance, $"{where} blue {expected} {actual}");
        Assert.True(Math.Abs(expected.Green - actual.Green) <= Tolerance, $"{where} green {expected} {actual}");
        Assert.True(Math.Abs(expected.Red - actual.Red) <= Tolerance, $"{where} red {expected} {actual}");
        Assert.True(Math.Abs(expected.Alpha - actual.Alpha) <= Tolerance, $"{where} alpha {expected} {actual}");
    }

    static void AssertFollowsTheModel(Rendering rendering, Model model, string where)
        => Assert.All(rendering.Coordinates(), point => AssertClose(model.Expected(point.X, point.Y), rendering[point.X, point.Y], $"{where} {point}"));

    static int ChangedPixels(Rendering rendering, SourceImage source)
        => rendering.Coordinates().Count(point => rendering[point.X, point.Y] != source[point.X, point.Y]);

    static double Focal(double fieldOfView = 3d) => MirageHorizon.FocalLength(Width, MirageSettings.GetHalfFieldOfViewTangent(fieldOfView));

    static ControllerPoint[] Line(IVideoEffectProcessor processor, int frame = 0)
        => Assert.Single(processor.Update(EffectDescriptions.At(frame, Length)).Controllers).Points.ToArray();

    [Fact]
    public void TheProcessorOnlyAddsTheControllerToTheDrawDescription()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = Inferior().CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var description = EffectDescriptions.At(0, Length);

        var draw = processor.Update(description);

        Assert.Equal(description.DrawDescription with { Controllers = draw.Controllers }, draw);
        Assert.Single(draw.Controllers);
    }

    [Fact]
    public void TheControllerIsALineAcrossTheImageAtTheHorizonAndBelongsToTheEffect()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        effect.HorizonPosition.Values[0].Value = 40d;
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var draw = processor.Update(EffectDescriptions.At(0, Length));

        var controller = Assert.IsType<VideoEffectController>(Assert.Single(draw.Controllers));
        Assert.Same(effect, controller.Owner);
        Assert.Equal(VideoControllerPointConnection.Line, controller.Connection);
        var points = controller.Points.ToArray();
        Assert.Equal(2, points.Length);
        Assert.Equal(new Vector3(0f, Height / 2f + 40f, 0f), points[0].Position);
        Assert.Equal(new Vector3(Width, Height / 2f + 40f, 0f), points[1].Position);
    }

    [Fact]
    public void TheLineSitsWhereTheTiltAndThePositionPutTheHorizon()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        effect.CameraPitch.Values[0].Value = 0.2d;
        effect.HorizonPosition.Values[0].Value = 40d;
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var line = Line(processor);

        var pitch = 0.2 * MirageSettings.RadiansPerDegree + Math.Atan(40d / Focal());
        Assert.Equal(Height / 2f + (float)(Focal() * Math.Tan(pitch)), line[0].Position.Y, 1e-3f);
        Assert.Equal(line[0].Position.Y, line[1].Position.Y);
    }

    [Fact]
    public void DraggingTheLineMovesTheHorizonOnTheNextFrame()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var point = Line(processor)[0];

        point.OnDrag!(new ControlPointDragEventArgs(new Vector3(7f, 9f, 0f), ModifierKeys.None));

        Assert.Equal(9d, effect.HorizonPosition.GetValue(0, Length, EffectDescriptions.Fps), 1e-9);
        var moved = Line(processor);
        Assert.Equal(Height / 2f + 9f, moved[0].Position.Y, 1e-3f);
        Assert.Equal(Height / 2f + 9f, moved[1].Position.Y, 1e-3f);
        var rendering = Rendering.Capture(context, processor.Output);
        AssertFollowsTheModel(rendering, new Model(effect, 0, Pattern), "after the drag");
    }

    [Fact]
    public void DraggingEitherEndOfTheLineMovesTheSameHorizon()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var first = Line(processor);
        first[0].OnDrag!(new ControlPointDragEventArgs(new Vector3(0f, 5f, 0f), ModifierKeys.None));
        var second = Line(processor);

        second[1].OnDrag!(new ControlPointDragEventArgs(new Vector3(0f, -12f, 0f), ModifierKeys.None));

        Assert.Equal(-7d, effect.HorizonPosition.GetValue(0, Length, EffectDescriptions.Fps), 1e-9);
    }

    [Fact]
    public void DraggingTheLineSidewaysLeavesTheHorizonAlone()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        effect.HorizonPosition.Values[0].Value = 15d;
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var point = Line(processor)[1];

        point.OnDrag!(new ControlPointDragEventArgs(new Vector3(30f, 0f, 0f), ModifierKeys.None));

        Assert.Equal(15d, effect.HorizonPosition.GetValue(0, Length, EffectDescriptions.Fps), 1e-9);
    }

    [Fact]
    public void ADragThatIsNotANumberLeavesTheHorizonAlone()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        effect.HorizonPosition.Values[0].Value = 15d;
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var point = Line(processor)[0];

        point.OnDrag!(new ControlPointDragEventArgs(new Vector3(0f, float.NaN, 0f), ModifierKeys.None));

        Assert.Equal(15d, effect.HorizonPosition.GetValue(0, Length, EffectDescriptions.Fps), 1e-9);
    }

    [Fact]
    public void WithATiltTheLineStillFollowsTheDragExactly()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        effect.CameraPitch.Values[0].Value = 0.3d;
        effect.HorizonPosition.Values[0].Value = 25d;
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var before = Line(processor);

        before[0].OnDrag!(new ControlPointDragEventArgs(new Vector3(0f, 11f, 0f), ModifierKeys.None));

        var after = Line(processor);
        Assert.Equal(before[0].Position.Y + 11f, after[0].Position.Y, 1e-3f);
        Assert.NotEqual(25d, effect.HorizonPosition.GetValue(0, Length, EffectDescriptions.Fps), 1e-3);
    }

    [Fact]
    public void DraggingAnAnimatedPositionAddsTheMovementToEveryValue()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        effect.HorizonPosition.CopyFrom(Linear(-20d, 20d));
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var first = effect.HorizonPosition.GetValue(0, Length, EffectDescriptions.Fps);
        var last = effect.HorizonPosition.GetValue(Length - 1, Length, EffectDescriptions.Fps);
        var point = Line(processor)[0];

        point.OnDrag!(new ControlPointDragEventArgs(new Vector3(0f, 6f, 0f), ModifierKeys.None));

        Assert.Equal(first + 6d, effect.HorizonPosition.GetValue(0, Length, EffectDescriptions.Fps), 1e-9);
        Assert.Equal(last + 6d, effect.HorizonPosition.GetValue(Length - 1, Length, EffectDescriptions.Fps), 1e-9);
    }

    [Fact]
    public void TheLineFollowsAnAnimatedPosition()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        effect.HorizonPosition.CopyFrom(Linear(-30d, 30d));
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var first = Line(processor, 0)[0];
        var last = Line(processor, Length - 1)[0];

        Assert.Equal(Height / 2f - 30f, first.Position.Y, 1e-3f);
        Assert.Equal(Height / 2f + (float)effect.HorizonPosition.GetValue(Length - 1, Length, EffectDescriptions.Fps), last.Position.Y, 1e-3f);
        Assert.True(last.Position.Y > first.Position.Y + 50f);
    }

    [Fact]
    public void TheLineStaysWithoutAnAmount()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = Inferior(0d).CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var draw = processor.Update(EffectDescriptions.At(0, Length));

        Assert.Equal(2, Assert.Single(draw.Controllers).Points.Count());
    }

    [Fact]
    public void TheControllersOfEarlierEffectsAreKeptInFront()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Pattern);
        var effect = Inferior();
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var earlier = new VideoEffectController(new MirageEffect(), [new ControllerPoint(Vector3.Zero)]);
        var description = EffectDescriptions.At(0, Length);
        description = description with { DrawDescription = description.DrawDescription with { Controllers = ImmutableList.Create<VideoEffectController>(earlier) } };

        var draw = processor.Update(description);

        Assert.Equal(2, draw.Controllers.Count);
        Assert.Same(earlier, draw.Controllers[0]);
        Assert.Same(effect, draw.Controllers[1].Owner);
    }

    [Fact]
    public void WithoutAnInputNoControllerIsAdded()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var processor = Inferior().CreateVideoEffect(context);

        var draw = processor.Update(EffectDescriptions.At(0, Length));

        Assert.Empty(draw.Controllers);
    }

    [Fact]
    public void ALargerImageGetsALineAcrossItsWholeWidth()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width * 2, Height * 2, Pattern);
        using var processor = Inferior().CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var line = Line(processor);

        Assert.Equal(new Vector3(0f, Height, 0f), line[0].Position);
        Assert.Equal(new Vector3(Width * 2, Height, 0f), line[1].Position);
    }

    [Theory]
    [InlineData(MirageKind.Inferior)]
    [InlineData(MirageKind.Superior)]
    public void WithoutAmountTheProcessorPassesTheImageThrough(MirageKind kind)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = kind == MirageKind.Inferior ? Inferior(0d) : Superior(0d);
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        Assert.Equal((0, 0, Width, Height), (rendering.Left, rendering.Top, rendering.Width, rendering.Height));
        Assert.All(rendering.Coordinates(), point => Assert.Equal(source[point.X, point.Y], rendering[point.X, point.Y]));
    }

    [Fact]
    public void EveryPixelOfTheInferiorMirageFollowsTheModelOfTheShader()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        AssertFollowsTheModel(rendering, new Model(effect, 0, Pattern), "inferior");
        Assert.True(ChangedPixels(rendering, source) > Width * 20);
    }

    [Theory]
    [InlineData(MirageSource.Photo)]
    [InlineData(MirageSource.Scene)]
    public void EveryPixelOfTheSuperiorMirageFollowsTheModelOfTheShader(MirageSource sourceKind)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Superior();
        effect.Source = sourceKind;
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        AssertFollowsTheModel(rendering, new Model(effect, 0, Pattern), $"superior {sourceKind}");
        Assert.True(ChangedPixels(rendering, source) > Width * 4);
    }

    [Fact]
    public void ThePhotoModeSmoothsTheHeightsTheStandardAtmosphereHidesAlongTheRow()
    {
        const int WideWidth = 800;
        const int WideHeight = 96;
        static Bgra Stripes(int x, int y) => Bgra.Opaque((byte)(x % 4 < 2 ? 0 : 240), (byte)(y * 2), 90);
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Superior();
        using var source = new SourceImage(context, WideWidth, WideHeight, Stripes);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        AssertFollowsTheModel(rendering, new Model(effect, 0, Stripes, WideWidth, WideHeight), "wide photo");
        var rows = Enumerable.Range(0, WideHeight).Where(y => Enumerable.Range(100, 400).Select(x => (int)rendering[x, y].Blue).Max() - Enumerable.Range(100, 400).Select(x => (int)rendering[x, y].Blue).Min() < 90 && rendering[100, y].Blue != source[100, y].Blue).ToArray();
        Assert.NotEmpty(rows);
    }

    [Fact]
    public void TheGroundBelowTheCriticalAngleOfTheInferiorMirageIsLeftAlone()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = Inferior().CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        var focal = 0.5 * Width / MirageSettings.GetHalfFieldOfViewTangent(3d);
        var firstUntouched = (int)Math.Ceiling(Height * 0.5 + focal * Math.Tan(0.39 * MirageSettings.RadiansPerDegree));
        Assert.True(firstUntouched < Height);
        Assert.All(rendering.Coordinates().Where(point => point.Y >= firstUntouched), point => Assert.Equal(source[point.X, point.Y], rendering[point.X, point.Y]));
        Assert.Contains(rendering.Coordinates().Where(point => point.Y < firstUntouched), point => rendering[point.X, point.Y] != source[point.X, point.Y]);
    }

    [Fact]
    public void ReturningToAFrameReproducesItExactly()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        effect.EyeHeight.CopyFrom(Linear(0.6d, 8d));
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var first = RenderFrame(context, processor, 5);
        RenderFrame(context, processor, 20);
        var again = RenderFrame(context, processor, 5);

        Assert.True(first.SamePixelsAs(again));
    }

    [Fact]
    public void ANewProcessorDrawsTheSameMirageAsOneThatAlreadyDrewAnotherFrame()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        effect.HeatDifference.CopyFrom(Linear(8d, 55d));
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var used = effect.CreateVideoEffect(context);
        using var fresh = effect.CreateVideoEffect(context);
        used.SetInput(source.Bitmap);
        fresh.SetInput(source.Bitmap);

        RenderFrame(context, used, 0);
        var reused = RenderFrame(context, used, 25);
        var created = RenderFrame(context, fresh, 25);

        Assert.True(reused.SamePixelsAs(created));
    }

    [Fact]
    public void AnimatedEyeHeightIsReadAtEachFrame()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        effect.EyeHeight.CopyFrom(Linear(0.6d, 8d));
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var start = RenderFrame(context, processor, 0);
        var end = RenderFrame(context, processor, Length - 1);

        Assert.False(start.SamePixelsAs(end));
        AssertFollowsTheModel(start, new Model(effect, 0, Pattern), "start");
        AssertFollowsTheModel(end, new Model(effect, Length - 1, Pattern), "end");
    }

    public static readonly TheoryData<string, MirageKind, Action<MirageEffect>> Changes = new()
    {
        { nameof(MirageEffect.Amount), MirageKind.Inferior, effect => effect.Amount.Values[0].Value = 40d },
        { nameof(MirageEffect.EyeHeight), MirageKind.Inferior, effect => effect.EyeHeight.Values[0].Value = 6d },
        { nameof(MirageEffect.HeatDifference), MirageKind.Inferior, effect => effect.HeatDifference.Values[0].Value = 45d },
        { nameof(MirageEffect.HeatThickness), MirageKind.Inferior, effect => effect.HeatThickness.Values[0].Value = 1.5d },
        { nameof(MirageEffect.FieldOfView), MirageKind.Inferior, effect => effect.FieldOfView.Values[0].Value = 1.5d },
        { nameof(MirageEffect.CameraPitch), MirageKind.Inferior, effect => effect.CameraPitch.Values[0].Value = 0.2d },
        { nameof(MirageEffect.HorizonPosition), MirageKind.Inferior, effect => effect.HorizonPosition.Values[0].Value = 30d },
        { nameof(MirageEffect.HorizonPosition), MirageKind.Superior, effect => effect.HorizonPosition.Values[0].Value = -20d },
        { nameof(MirageEffect.Shimmer), MirageKind.Inferior, effect => effect.Shimmer.Values[0].Value = 60d },
        { nameof(MirageEffect.Shimmer), MirageKind.Superior, effect => effect.Shimmer.Values[0].Value = 100d },
        { nameof(MirageEffect.Kind), MirageKind.Inferior, effect => effect.Kind = MirageKind.Superior },
        { nameof(MirageEffect.Kind), MirageKind.Superior, effect => effect.Kind = MirageKind.Inferior },
        { nameof(MirageEffect.EyeHeight), MirageKind.Superior, effect => effect.EyeHeight.Values[0].Value = 6d },
        { nameof(MirageEffect.InversionDifference), MirageKind.Superior, effect => effect.InversionDifference.Values[0].Value = 6d },
        { nameof(MirageEffect.InversionHeight), MirageKind.Superior, effect => effect.InversionHeight.Values[0].Value = 100d },
        { nameof(MirageEffect.InversionThickness), MirageKind.Superior, effect => effect.InversionThickness.Values[0].Value = 4d },
        { nameof(MirageEffect.Distance), MirageKind.Superior, effect => effect.Distance.Values[0].Value = 20d },
        { nameof(MirageEffect.Source), MirageKind.Superior, effect => effect.Source = MirageSource.Scene },
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public void EverySettingChangedAfterTheFirstFrameReachesTheShader(string setting, MirageKind kind, Action<MirageEffect> change)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = kind == MirageKind.Inferior ? Inferior() : Superior();
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var before = RenderFrame(context, processor, 3);
        change(effect);
        var after = RenderFrame(context, processor, 3);

        Assert.False(before.SamePixelsAs(after), setting);
        AssertFollowsTheModel(after, new Model(effect, 3, Pattern), $"{setting} {kind}");
    }

    [Fact]
    public void AHorizonPositionMovesTheMirageLikeTheMatchingTilt()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var positioned = Inferior();
        positioned.HorizonPosition.Values[0].Value = 30d;
        var focal = MirageHorizon.FocalLength(Width, MirageSettings.GetHalfFieldOfViewTangent(3d));
        var tilted = Inferior();
        tilted.CameraPitch.Values[0].Value = Math.Atan(30d / focal) / MirageSettings.RadiansPerDegree;
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var positionedProcessor = positioned.CreateVideoEffect(context);
        using var tiltedProcessor = tilted.CreateVideoEffect(context);
        positionedProcessor.SetInput(source.Bitmap);
        tiltedProcessor.SetInput(source.Bitmap);

        var first = RenderFrame(context, positionedProcessor, 0);
        var second = RenderFrame(context, tiltedProcessor, 0);

        Assert.True(first.SamePixelsAs(second));
        Assert.True(ChangedPixels(first, source) > Width * 20);
    }

    [Fact]
    public void AHorizonPositionAddsToTheTiltInAWideView()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var positioned = Inferior();
        positioned.FieldOfView.Values[0].Value = 60d;
        positioned.CameraPitch.Values[0].Value = 3d;
        positioned.HorizonPosition.Values[0].Value = 30d;
        var focal = MirageHorizon.FocalLength(Width, MirageSettings.GetHalfFieldOfViewTangent(60d));
        var tilted = Inferior();
        tilted.FieldOfView.Values[0].Value = 60d;
        tilted.CameraPitch.Values[0].Value = (3d * MirageSettings.RadiansPerDegree + Math.Atan(30d / focal)) / MirageSettings.RadiansPerDegree;
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var positionedProcessor = positioned.CreateVideoEffect(context);
        using var tiltedProcessor = tilted.CreateVideoEffect(context);
        positionedProcessor.SetInput(source.Bitmap);
        tiltedProcessor.SetInput(source.Bitmap);

        var first = RenderFrame(context, positionedProcessor, 0);
        var second = RenderFrame(context, tiltedProcessor, 0);

        Assert.True(first.SamePixelsAs(second));
        Assert.True(ChangedPixels(first, source) > 0);
    }

    [Fact]
    public void TheShimmerMovesTheMirageFromFrameToFrame()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        effect.Shimmer.Values[0].Value = 80d;
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var first = RenderFrame(context, processor, 2);
        var second = RenderFrame(context, processor, 9);
        var again = RenderFrame(context, processor, 2);

        Assert.False(first.SamePixelsAs(second));
        Assert.True(first.SamePixelsAs(again));
        AssertFollowsTheModel(first, new Model(effect, 2, Pattern), "frame 2");
        AssertFollowsTheModel(second, new Model(effect, 9, Pattern), "frame 9");
    }

    [Fact]
    public void WithoutShimmerTheMirageStaysStillAcrossFrames()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = Inferior().CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var first = RenderFrame(context, processor, 1);
        var second = RenderFrame(context, processor, 17);

        Assert.True(first.SamePixelsAs(second));
    }

    [Fact]
    public void TheShimmerSpeedChangesWhereTheWavesAreAtALaterFrame()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        effect.Shimmer.Values[0].Value = 80d;
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var slow = RenderFrame(context, processor, 12);
        effect.ShimmerSpeed.Values[0].Value = 3d;
        var fast = RenderFrame(context, processor, 12);

        Assert.False(slow.SamePixelsAs(fast));
        AssertFollowsTheModel(fast, new Model(effect, 12, Pattern), "fast");
    }

    [Fact]
    public void AShimmerSpeedOfZeroFreezesTheWaves()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        effect.Shimmer.Values[0].Value = 80d;
        effect.ShimmerSpeed.Values[0].Value = 0d;
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var first = RenderFrame(context, processor, 0);
        var later = RenderFrame(context, processor, 20);

        Assert.True(first.SamePixelsAs(later));
        Assert.False(first.SamePixelsAs(Rendering.Capture(context, source.Bitmap)));
    }

    [Fact]
    public void RaisingTheAmountFromZeroOnTheFirstFrameDrawsTheMirage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior(0d);
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var hidden = RenderFrame(context, processor, 0);
        effect.Amount.Values[0].Value = 100d;
        var shown = RenderFrame(context, processor, 0);

        Assert.All(hidden.Coordinates(), point => Assert.Equal(source[point.X, point.Y], hidden[point.X, point.Y]));
        AssertFollowsTheModel(shown, new Model(effect, 0, Pattern), "shown");
    }

    [Fact]
    public void ReducingTheAmountToZeroRestoresTheSource()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var shown = RenderFrame(context, processor, 0);
        effect.Amount.Values[0].Value = 0d;
        var hidden = RenderFrame(context, processor, 0);

        Assert.False(shown.SamePixelsAs(hidden));
        Assert.All(hidden.Coordinates(), point => Assert.Equal(source[point.X, point.Y], hidden[point.X, point.Y]));
    }

    [Fact]
    public void ADifferentKindOnTheFirstFrameBuildsItsOwnMap()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Superior();
        using var source = new SourceImage(context, Width, Height, Pattern);
        using var first = effect.CreateVideoEffect(context);
        first.SetInput(source.Bitmap);
        var inferior = Inferior();
        using var second = inferior.CreateVideoEffect(context);
        second.SetInput(source.Bitmap);

        var superior = RenderFrame(context, first, 0);
        var other = RenderFrame(context, second, 0);

        Assert.False(superior.SamePixelsAs(other));
        AssertFollowsTheModel(superior, new Model(effect, 0, Pattern), "superior");
        AssertFollowsTheModel(other, new Model(inferior, 0, Pattern), "inferior");
    }

    [Fact]
    public void TransparentColumnsStayTransparent()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        using var source = new SourceImage(context, Width, Height, TransparentColumns);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        Assert.All(rendering.Coordinates().Where(point => point.X < Width / 2), point => Assert.Equal(Bgra.Transparent, rendering[point.X, point.Y]));
        AssertFollowsTheModel(rendering, new Model(effect, 0, TransparentColumns), "transparent");
    }

    [Fact]
    public void ASemiTransparentSourceKeepsItsPremultipliedColors()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        using var source = new SourceImage(context, Width, Height, HalfTransparent);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        AssertFollowsTheModel(rendering, new Model(effect, 0, HalfTransparent), "semi-transparent");
        Assert.All(rendering.Coordinates(), point =>
        {
            Assert.InRange(rendering[point.X, point.Y].Blue, 0, rendering[point.X, point.Y].Alpha);
            Assert.InRange(rendering[point.X, point.Y].Green, 0, rendering[point.X, point.Y].Alpha);
            Assert.InRange(rendering[point.X, point.Y].Red, 0, rendering[point.X, point.Y].Alpha);
        });
    }

    [Fact]
    public void TheImageSizeOnlyScalesTheMirageAndKeepsItsAngularSize()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Inferior();
        Bgra Smooth(int x, int y, int height) => Bgra.Opaque((byte)(200.0 * (y + 0.5) / height), 0, 0);
        using var small = new SourceImage(context, Width, Height, (x, y) => Smooth(x, y, Height));
        using var large = new SourceImage(context, Width * 2, Height * 2, (x, y) => Smooth(x, y, Height * 2));
        using var smallProcessor = effect.CreateVideoEffect(context);
        using var largeProcessor = effect.CreateVideoEffect(context);
        smallProcessor.SetInput(small.Bitmap);
        largeProcessor.SetInput(large.Bitmap);

        var first = RenderFrame(context, smallProcessor, 0);
        var second = RenderFrame(context, largeProcessor, 0);

        Assert.All(Enumerable.Range(0, Height), y =>
        {
            var expected = first[Width / 2, y].Blue;
            var actual = 0.5 * (second[Width, 2 * y].Blue + second[Width, 2 * y + 1].Blue);
            Assert.True(Math.Abs(expected - actual) <= 4.0, $"row {y} {expected} {actual}");
        });
        Assert.True(ChangedPixels(first, small) > Width * 20);
    }

    [Fact]
    public void AFailureWhileUpdatingIsNotSwallowed()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var processor = Inferior().CreateVideoEffect(context);

        Assert.ThrowsAny<Exception>(() => processor.Update(null!));
    }

    [Fact]
    public void AFailureWhileCreatingTheProcessorIsNotSwallowed()
    {
        Assert.ThrowsAny<Exception>(() => Inferior().CreateVideoEffect(null!));
    }
}
