using System.Numerics;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace Mirage;

internal sealed class MirageCustomEffect(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
{
    internal enum PropertyIndex
    {
        Amount = 0,
        HalfFieldTangent,
        PitchCosine,
        PitchSine,
        ShimmerAmplitude,
        ShimmerPhase0,
        ShimmerPhase1,
        ShimmerPhase2,
        ShimmerPhase3,
    }

    public float Amount { set => SetValue((int)PropertyIndex.Amount, value); }
    public float HalfFieldTangent { set => SetValue((int)PropertyIndex.HalfFieldTangent, value); }
    public float PitchCosine { set => SetValue((int)PropertyIndex.PitchCosine, value); }
    public float PitchSine { set => SetValue((int)PropertyIndex.PitchSine, value); }
    public float ShimmerAmplitude { set => SetValue((int)PropertyIndex.ShimmerAmplitude, value); }
    public float ShimmerPhase0 { set => SetValue((int)PropertyIndex.ShimmerPhase0, value); }
    public float ShimmerPhase1 { set => SetValue((int)PropertyIndex.ShimmerPhase1, value); }
    public float ShimmerPhase2 { set => SetValue((int)PropertyIndex.ShimmerPhase2, value); }
    public float ShimmerPhase3 { set => SetValue((int)PropertyIndex.ShimmerPhase3, value); }

    private const float DefaultHalfFieldTangent = 1f;
    private const float MaximumShimmerAmplitude = 0.01f;
    private const float MaximumShimmerPhase = 1e4f;

    [CustomEffect(2)]
    internal sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
    {
        private ConstantBuffer _cb;
        private RawRect _mapRect = new(0, 0, MirageSettings.MapWidth, 1);

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.Amount)]
        public float Amount { get => _cb.Amount; set { _cb.Amount = Sanitize(value, 0f, 1f); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.HalfFieldTangent)]
        public float HalfFieldTangent { get => _cb.HalfFieldTangent; set { _cb.HalfFieldTangent = float.IsNaN(value) ? DefaultHalfFieldTangent : Math.Clamp(value, MirageSettings.MinimumHalfFieldTangent, MirageSettings.MaximumHalfFieldTangent); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.PitchCosine)]
        public float PitchCosine { get => _cb.PitchCosine; set { _cb.PitchCosine = Sanitize(value, -1f, 1f); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.PitchSine)]
        public float PitchSine { get => _cb.PitchSine; set { _cb.PitchSine = Sanitize(value, -1f, 1f); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ShimmerAmplitude)]
        public float ShimmerAmplitude { get => _cb.ShimmerAmplitude; set { _cb.ShimmerAmplitude = Sanitize(value, 0f, MaximumShimmerAmplitude); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ShimmerPhase0)]
        public float ShimmerPhase0 { get => _cb.ShimmerPhase.X; set { _cb.ShimmerPhase.X = Sanitize(value, -MaximumShimmerPhase, MaximumShimmerPhase); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ShimmerPhase1)]
        public float ShimmerPhase1 { get => _cb.ShimmerPhase.Y; set { _cb.ShimmerPhase.Y = Sanitize(value, -MaximumShimmerPhase, MaximumShimmerPhase); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ShimmerPhase2)]
        public float ShimmerPhase2 { get => _cb.ShimmerPhase.Z; set { _cb.ShimmerPhase.Z = Sanitize(value, -MaximumShimmerPhase, MaximumShimmerPhase); UpdateConstants(); } }

        [CustomEffectProperty(PropertyType.Float, (int)PropertyIndex.ShimmerPhase3)]
        public float ShimmerPhase3 { get => _cb.ShimmerPhase.W; set { _cb.ShimmerPhase.W = Sanitize(value, -MaximumShimmerPhase, MaximumShimmerPhase); UpdateConstants(); } }

        public EffectImpl() : base(ShaderResourceUri.Get("Mirage"))
        {
            _cb.HalfFieldTangent = DefaultHalfFieldTangent;
            _cb.PitchCosine = 1f;
            _cb.MapStart = (float)MirageSettings.GetMapStartRadians();
            _cb.MapSpan = (float)MirageSettings.GetMapSpanRadians();
            _cb.MapWidth = MirageSettings.MapWidth;
        }

        private static float Sanitize(float value, float minimum, float maximum)
            => float.IsNaN(value) ? 0f : Math.Clamp(value, minimum, maximum);

        protected override void UpdateConstants()
        {
            if (drawInformation is null)
                return;

            try
            {
                drawInformation.SetPixelShaderConstantBuffer(_cb);
            }
            catch (Exception exception)
            {
                MirageTelemetry.Report(exception);
                throw;
            }
        }

        public override void MapInputRectsToOutputRect(
            RawRect[] inputRects,
            RawRect[] inputOpaqueSubRects,
            out RawRect outputRect,
            out RawRect outputOpaqueSubRect)
        {
            inputRect = ClampInputRect(inputRects[0]);
            if (inputRects.Length > 1)
                _mapRect = inputRects[1];
            outputRect = inputRect;
            outputOpaqueSubRect = default;
            if (inputRect.Right <= inputRect.Left || inputRect.Bottom <= inputRect.Top)
                return;

            _cb.InputBounds = new Vector4(inputRect.Left, inputRect.Top, inputRect.Right, inputRect.Bottom);
            UpdateConstants();
        }

        public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
        {
            if (inputRects.Length > 0)
                inputRects[0] = inputRect;
            if (inputRects.Length > 1)
                inputRects[1] = _mapRect;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ConstantBuffer
        {
            public Vector4 InputBounds;
            public float HalfFieldTangent;
            public float PitchCosine;
            public float PitchSine;
            public float Amount;
            public float MapStart;
            public float MapSpan;
            public float MapWidth;
            public float Padding;
            public Vector4 ShimmerPhase;
            public float ShimmerAmplitude;
            public float Padding0;
            public float Padding1;
            public float Padding2;
        }
    }
}
