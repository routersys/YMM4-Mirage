using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using Vortice.DCommon;
using Vortice.DXGI;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace Mirage;

internal sealed class MirageEffectProcessor(IGraphicsDevicesAndContext devices, MirageEffect item) : VideoEffectProcessorBase(devices)
{
    private readonly MirageEffect _item = item;
    private MirageCustomEffect? _effect;
    private ID2D1Bitmap1? _map;
    private double[]? _standard;
    private bool _isFirst = true;
    private StandardKey _standardKey;
    private MapKey _mapKey;
    private ConstantKey _constantKey;

    public override DrawDescription Update(EffectDescription effectDescription)
    {
        try
        {
            return UpdateCore(effectDescription);
        }
        catch (Exception exception)
        {
            MirageTelemetry.Report(exception);
            throw;
        }
    }

    private DrawDescription UpdateCore(EffectDescription effectDescription)
    {
        if (IsPassThroughEffect || _effect is null || _map is null)
            return effectDescription.DrawDescription;

        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;
        var amount = (float)(_item.Amount.GetValue(frame, length, fps) / 100.0);
        if (_isFirst || _constantKey.Amount != amount)
            _effect.Amount = amount;

        if (amount <= 0f)
        {
            _constantKey = _constantKey with { Amount = amount };
            _isFirst = false;
            return effectDescription.DrawDescription;
        }

        var eyeHeight = Math.Clamp(_item.EyeHeight.GetValue(frame, length, fps), MirageSettings.MinimumEyeHeightMeters, MirageSettings.MaximumEyeHeightMeters);
        var mapKey = _item.Kind == MirageKind.Inferior
            ? new MapKey(MirageKind.Inferior, eyeHeight, Math.Clamp(_item.HeatDifference.GetValue(frame, length, fps), MirageSettings.MinimumHeatDifference, MirageSettings.MaximumHeatDifference), Math.Clamp(_item.HeatThickness.GetValue(frame, length, fps), MirageSettings.MinimumHeatThicknessMeters, MirageSettings.MaximumHeatThicknessMeters), 0.0, 0.0, 0.0, 0.0, default)
            : new MapKey(MirageKind.Superior, eyeHeight, 0.0, 0.0, Math.Clamp(_item.InversionDifference.GetValue(frame, length, fps), MirageSettings.MinimumInversionDifference, MirageSettings.MaximumInversionDifference), Math.Clamp(_item.InversionHeight.GetValue(frame, length, fps), MirageSettings.MinimumInversionHeightMeters, MirageSettings.MaximumInversionHeightMeters), Math.Clamp(_item.InversionThickness.GetValue(frame, length, fps), MirageSettings.MinimumInversionThicknessMeters, MirageSettings.MaximumInversionThicknessMeters), Math.Clamp(_item.Distance.GetValue(frame, length, fps), MirageSettings.MinimumDistanceKilometers, MirageSettings.MaximumDistanceKilometers), _item.Source);
        if (_isFirst || _mapKey != mapKey)
        {
            WriteMap(Build(mapKey));
            _mapKey = mapKey;
        }

        var pitch = Math.Clamp(_item.CameraPitch.GetValue(frame, length, fps), MirageSettings.MinimumCameraPitchDegrees, MirageSettings.MaximumCameraPitchDegrees) * MirageSettings.RadiansPerDegree;
        var constantKey = new ConstantKey(amount, MirageSettings.GetHalfFieldOfViewTangent(_item.FieldOfView.GetValue(frame, length, fps)), (float)Math.Cos(pitch), (float)Math.Sin(pitch));
        if (_isFirst || _constantKey.HalfFieldTangent != constantKey.HalfFieldTangent)
            _effect.HalfFieldTangent = constantKey.HalfFieldTangent;
        if (_isFirst || _constantKey.PitchCosine != constantKey.PitchCosine)
            _effect.PitchCosine = constantKey.PitchCosine;
        if (_isFirst || _constantKey.PitchSine != constantKey.PitchSine)
            _effect.PitchSine = constantKey.PitchSine;

        _constantKey = constantKey;
        _isFirst = false;
        return effectDescription.DrawDescription;
    }

    private float[] Build(MapKey key)
    {
        if (key.Kind == MirageKind.Inferior)
            return MirageTable.ToTexels(MirageTable.BuildInferior(key.EyeHeight, key.HeatDifference, key.HeatThickness));

        var distanceMeters = key.Distance * MirageSettings.MetersPerKilometer;
        var standardKey = new StandardKey(key.EyeHeight, distanceMeters);
        if (_standard is null || _standardKey != standardKey)
        {
            _standard = MirageTable.BuildStandard(key.EyeHeight, distanceMeters);
            _standardKey = standardKey;
        }

        var shifts = MirageTable.BuildSuperior(key.EyeHeight, key.InversionDifference, key.InversionHeight, key.InversionThickness, distanceMeters, key.Source, _standard, out var fill);
        return MirageTable.ToTexels(shifts, fill);
    }

    private void WriteMap(float[] texels)
    {
        var handle = GCHandle.Alloc(texels, GCHandleType.Pinned);
        try
        {
            _map!.CopyFromMemory(handle.AddrOfPinnedObject(), MirageSettings.MapWidth * MirageSettings.TexelBytes);
        }
        finally
        {
            handle.Free();
        }
    }

    protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
    {
        var effect = new MirageCustomEffect(devices);
        if (!effect.IsEnabled)
        {
            effect.Dispose();
            return null;
        }

        _effect = effect;
        disposer.Collect(effect);
        var properties = new BitmapProperties1(new PixelFormat(Format.R32G32B32A32_Float, Vortice.DCommon.AlphaMode.Premultiplied), 96f, 96f, BitmapOptions.None);
        var empty = new float[MirageSettings.MapWidth * MirageSettings.TexelChannels];
        var handle = GCHandle.Alloc(empty, GCHandleType.Pinned);
        try
        {
            _map = ((ID2D1DeviceContext1)devices.DeviceContext).CreateBitmap(
                new Vortice.Mathematics.SizeI(MirageSettings.MapWidth, 1),
                handle.AddrOfPinnedObject(),
                MirageSettings.MapWidth * MirageSettings.TexelBytes,
                properties);
        }
        finally
        {
            handle.Free();
        }

        disposer.Collect(_map);
        _effect.SetInput(1, _map, true);
        var output = effect.Output;
        disposer.Collect(output);
        return output;
    }

    protected override void setInput(ID2D1Image? input)
    {
        _effect?.SetInput(0, input, true);
    }

    protected override void ClearEffectChain()
    {
        try
        {
            _effect?.SetInput(0, null, true);
            _isFirst = true;
        }
        catch (Exception exception)
        {
            MirageTelemetry.Report(exception);
            throw;
        }
    }

    private readonly record struct StandardKey(double EyeHeight, double Distance);

    private readonly record struct MapKey(MirageKind Kind, double EyeHeight, double HeatDifference, double HeatThickness, double InversionDifference, double InversionHeight, double InversionThickness, double Distance, MirageSource Source);

    private readonly record struct ConstantKey(float Amount, float HalfFieldTangent, float PitchCosine, float PitchSine);
}
