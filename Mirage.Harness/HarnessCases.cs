using System.Globalization;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Json;

namespace Mirage.Harness;

internal static class HarnessCases
{
    public static IEnumerable<(string Name, MirageEffect Effect, IReadOnlyList<int> Frames)> All()
    {
        yield return ("default", Create(), [0]);
        yield return ("eye-height-animated-frames", Create(effect => effect.EyeHeight.CopyFrom(Linear(0.6d, 8d))), [0, 100, 200, 299]);
        yield return ("heat-difference-animated-frames", Create(effect => effect.HeatDifference.CopyFrom(Linear(8d, 55d))), [0, 100, 200, 299]);
        yield return ("amount-0", Create(effect => effect.Amount.Values[0].Value = 0), [0]);
        yield return ("amount-50", Create(effect => effect.Amount.Values[0].Value = 50), [0]);
        yield return ("eye-height-10", Create(effect => effect.EyeHeight.Values[0].Value = 10), [0]);
        yield return ("heat-difference-8", Create(effect => effect.HeatDifference.Values[0].Value = 8), [0]);
        yield return ("heat-difference-55", Create(effect => effect.HeatDifference.Values[0].Value = 55), [0]);
        yield return ("heat-thickness-0.15", Create(effect => effect.HeatThickness.Values[0].Value = 0.15), [0]);
        yield return ("heat-thickness-2.5", Create(effect => effect.HeatThickness.Values[0].Value = 2.5), [0]);
        yield return ("field-of-view-1", Create(effect => effect.FieldOfView.Values[0].Value = 1), [0]);
        yield return ("field-of-view-12", Create(effect => effect.FieldOfView.Values[0].Value = 12), [0]);
        yield return ("camera-pitch-0.3", Create(effect => effect.CameraPitch.Values[0].Value = 0.3), [0]);
        yield return ("camera-pitch-minus-0.3", Create(effect => effect.CameraPitch.Values[0].Value = -0.3), [0]);
        yield return ("superior-default", Create(effect => effect.Kind = MirageKind.Superior), [0]);
        yield return ("superior-scene", Create(effect => { effect.Kind = MirageKind.Superior; effect.Source = MirageSource.Scene; }), [0]);
        yield return ("superior-inversion-difference-4", Create(effect => { effect.Kind = MirageKind.Superior; effect.InversionDifference.Values[0].Value = 4; }), [0]);
        yield return ("superior-inversion-difference-18", Create(effect => { effect.Kind = MirageKind.Superior; effect.InversionDifference.Values[0].Value = 18; }), [0]);
        yield return ("superior-inversion-height-25", Create(effect => { effect.Kind = MirageKind.Superior; effect.InversionHeight.Values[0].Value = 25; }), [0]);
        yield return ("superior-inversion-thickness-3", Create(effect => { effect.Kind = MirageKind.Superior; effect.InversionThickness.Values[0].Value = 3; }), [0]);
        yield return ("superior-distance-15", Create(effect => { effect.Kind = MirageKind.Superior; effect.Distance.Values[0].Value = 15; }), [0]);
        yield return ("superior-distance-80", Create(effect => { effect.Kind = MirageKind.Superior; effect.Distance.Values[0].Value = 80; }), [0]);
        yield return ("superior-eye-height-8", Create(effect => { effect.Kind = MirageKind.Superior; effect.EyeHeight.Values[0].Value = 8; }), [0]);
        yield return ("superior-inversion-difference-animated-frames", Create(effect => { effect.Kind = MirageKind.Superior; effect.InversionDifference.CopyFrom(Linear(3d, 18d)); }), [0, 100, 200, 299]);
    }

    public static IEnumerable<(string Name, Func<MirageEffect> Create, Action<MirageEffect> Change, int Frame)> Transitions()
    {
        yield return ("amount-100-to-0", () => Create(), effect => effect.Amount.Values[0].Value = 0, 0);
        yield return ("amount-0-to-100", () => Create(effect => effect.Amount.Values[0].Value = 0), effect => effect.Amount.Values[0].Value = 100, 0);
        yield return ("amount-100-to-50", () => Create(), effect => effect.Amount.Values[0].Value = 50, 0);
        yield return ("eye-height-2-to-6", () => Create(), effect => effect.EyeHeight.Values[0].Value = 6, 0);
        yield return ("heat-difference-25-to-45", () => Create(), effect => effect.HeatDifference.Values[0].Value = 45, 0);
        yield return ("heat-thickness-0.5-to-1.5", () => Create(), effect => effect.HeatThickness.Values[0].Value = 1.5, 0);
        yield return ("field-of-view-3-to-1.5", () => Create(), effect => effect.FieldOfView.Values[0].Value = 1.5, 0);
        yield return ("camera-pitch-0-to-0.2", () => Create(), effect => effect.CameraPitch.Values[0].Value = 0.2, 0);
        yield return ("kind-inferior-to-superior", () => Create(), effect => effect.Kind = MirageKind.Superior, 0);
        yield return ("kind-superior-to-inferior", () => Create(effect => effect.Kind = MirageKind.Superior), effect => effect.Kind = MirageKind.Inferior, 0);
        yield return ("source-photo-to-scene", () => Create(effect => effect.Kind = MirageKind.Superior), effect => effect.Source = MirageSource.Scene, 0);
        yield return ("inversion-difference-12-to-6", () => Create(effect => effect.Kind = MirageKind.Superior), effect => effect.InversionDifference.Values[0].Value = 6, 0);
        yield return ("inversion-height-60-to-100", () => Create(effect => effect.Kind = MirageKind.Superior), effect => effect.InversionHeight.Values[0].Value = 100, 0);
        yield return ("inversion-thickness-10-to-4", () => Create(effect => effect.Kind = MirageKind.Superior), effect => effect.InversionThickness.Values[0].Value = 4, 0);
        yield return ("distance-40-to-20", () => Create(effect => effect.Kind = MirageKind.Superior), effect => effect.Distance.Values[0].Value = 20, 0);
        yield return ("eye-height-2-to-6-superior", () => Create(effect => effect.Kind = MirageKind.Superior), effect => effect.EyeHeight.Values[0].Value = 6, 0);
    }

    public static IEnumerable<(string Name, MirageEffect Effect)> Benchmarks()
    {
        yield return ("default", Create());
        yield return ("superior-default", Create(effect => effect.Kind = MirageKind.Superior));
        yield return ("camera-pitch-animated", Create(effect => effect.CameraPitch.CopyFrom(Linear(-0.3d, 0.3d))));
        yield return ("amount-0", Create(effect => effect.Amount.Values[0].Value = 0));
    }

    static Animation Linear(double from, double to)
        => Json.LoadFromText<Animation>(string.Create(CultureInfo.InvariantCulture, $$"""{"AnimationType":"直線移動","Values":[{"Value":{{from}}},{"Value":{{to}}}]}""")) ?? throw new HarnessException("アニメーションを読み込めません。");

    public static MirageEffect Create(Action<MirageEffect>? configure = null)
    {
        var effect = new MirageEffect();
        configure?.Invoke(effect);
        return effect;
    }
}
