using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;

namespace Mirage;

[PluginDetails(AuthorName = "routersys")]
[VideoEffect(nameof(Texts.Mirage), [VideoEffectCategories.Filtering], [nameof(Texts.TagMirage), nameof(Texts.TagRefraction), nameof(Texts.TagAtmosphere)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class MirageEffect : VideoEffectBase
{
    public override string Label => Texts.Mirage;

    public MirageEffect()
    {
        MirageTelemetry.EnsureStartedOnce();
        MirageUpdateNotifier.EnsureCheckedOnce();
    }

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Amount), Description = nameof(Texts.AmountDescription), Order = 0, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Amount { get; } = new Animation(100, 0, 100);

    [Display(GroupName = nameof(Texts.AtmosphereGroup), Name = nameof(Texts.Kind), Description = nameof(Texts.KindDescription), Order = 10, ResourceType = typeof(Texts))]
    [EnumComboBox]
    public MirageKind Kind { get => _kind; set => Set(ref _kind, value); }
    private MirageKind _kind = MirageKind.Inferior;

    [Display(GroupName = nameof(Texts.AtmosphereGroup), Name = nameof(Texts.EyeHeight), Description = nameof(Texts.EyeHeightDescription), Order = 11, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "m", MirageSettings.MinimumEyeHeightMeters, MirageSettings.MaximumEyeHeightMeters)]
    public Animation EyeHeight { get; } = new Animation(2, MirageSettings.MinimumEyeHeightMeters, MirageSettings.MaximumEyeHeightMeters);

    [Display(GroupName = nameof(Texts.AtmosphereGroup), Name = nameof(Texts.HeatDifference), Description = nameof(Texts.HeatDifferenceDescription), Order = 12, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "K", MirageSettings.MinimumHeatDifference, MirageSettings.MaximumHeatDifference)]
    [ShowPropertyEditorWhen(nameof(Kind), MirageKind.Inferior)]
    public Animation HeatDifference { get; } = new Animation(25, MirageSettings.MinimumHeatDifference, MirageSettings.MaximumHeatDifference);

    [Display(GroupName = nameof(Texts.AtmosphereGroup), Name = nameof(Texts.HeatThickness), Description = nameof(Texts.HeatThicknessDescription), Order = 13, ResourceType = typeof(Texts))]
    [AnimationSlider("F2", "m", MirageSettings.MinimumHeatThicknessMeters, MirageSettings.MaximumHeatThicknessMeters)]
    [ShowPropertyEditorWhen(nameof(Kind), MirageKind.Inferior)]
    public Animation HeatThickness { get; } = new Animation(0.5, MirageSettings.MinimumHeatThicknessMeters, MirageSettings.MaximumHeatThicknessMeters);

    [Display(GroupName = nameof(Texts.AtmosphereGroup), Name = nameof(Texts.InversionDifference), Description = nameof(Texts.InversionDifferenceDescription), Order = 14, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "K", MirageSettings.MinimumInversionDifference, MirageSettings.MaximumInversionDifference)]
    [ShowPropertyEditorWhen(nameof(Kind), MirageKind.Superior)]
    public Animation InversionDifference { get; } = new Animation(12, MirageSettings.MinimumInversionDifference, MirageSettings.MaximumInversionDifference);

    [Display(GroupName = nameof(Texts.AtmosphereGroup), Name = nameof(Texts.InversionHeight), Description = nameof(Texts.InversionHeightDescription), Order = 15, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "m", MirageSettings.MinimumInversionHeightMeters, MirageSettings.MaximumInversionHeightMeters)]
    [ShowPropertyEditorWhen(nameof(Kind), MirageKind.Superior)]
    public Animation InversionHeight { get; } = new Animation(60, MirageSettings.MinimumInversionHeightMeters, MirageSettings.MaximumInversionHeightMeters);

    [Display(GroupName = nameof(Texts.AtmosphereGroup), Name = nameof(Texts.InversionThickness), Description = nameof(Texts.InversionThicknessDescription), Order = 16, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "m", MirageSettings.MinimumInversionThicknessMeters, MirageSettings.MaximumInversionThicknessMeters)]
    [ShowPropertyEditorWhen(nameof(Kind), MirageKind.Superior)]
    public Animation InversionThickness { get; } = new Animation(10, MirageSettings.MinimumInversionThicknessMeters, MirageSettings.MaximumInversionThicknessMeters);

    [Display(GroupName = nameof(Texts.AtmosphereGroup), Name = nameof(Texts.Distance), Description = nameof(Texts.DistanceDescription), Order = 17, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "km", MirageSettings.MinimumDistanceKilometers, MirageSettings.MaximumDistanceKilometers)]
    [ShowPropertyEditorWhen(nameof(Kind), MirageKind.Superior)]
    public Animation Distance { get; } = new Animation(40, MirageSettings.MinimumDistanceKilometers, MirageSettings.MaximumDistanceKilometers);

    [Display(GroupName = nameof(Texts.AtmosphereGroup), Name = nameof(Texts.Source), Description = nameof(Texts.SourceDescription), Order = 18, ResourceType = typeof(Texts))]
    [EnumComboBox]
    [ShowPropertyEditorWhen(nameof(Kind), MirageKind.Superior)]
    public MirageSource Source { get => _source; set => Set(ref _source, value); }
    private MirageSource _source = MirageSource.Photo;

    [Display(GroupName = nameof(Texts.AtmosphereGroup), Name = nameof(Texts.Shimmer), Description = nameof(Texts.ShimmerDescription), Order = 19, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", MirageSettings.MinimumShimmerPercent, MirageSettings.MaximumShimmerPercent)]
    public Animation Shimmer { get; } = new Animation(0, MirageSettings.MinimumShimmerPercent, MirageSettings.MaximumShimmerPercent);

    [Display(GroupName = nameof(Texts.AtmosphereGroup), Name = nameof(Texts.ShimmerSpeed), Description = nameof(Texts.ShimmerSpeedDescription), Order = 20, ResourceType = typeof(Texts))]
    [AnimationSlider("F2", "×", MirageSettings.MinimumShimmerSpeed, MirageSettings.MaximumShimmerSpeed)]
    public Animation ShimmerSpeed { get; } = new Animation(MirageSettings.DefaultShimmerSpeed, MirageSettings.MinimumShimmerSpeed, MirageSettings.MaximumShimmerSpeed);

    [Display(GroupName = nameof(Texts.CameraGroup), Name = nameof(Texts.FieldOfView), Description = nameof(Texts.FieldOfViewDescription), Order = 20, ResourceType = typeof(Texts))]
    [AnimationSlider("F2", "°", MirageSettings.MinimumFieldOfViewDegrees, MirageSettings.MaximumFieldOfViewDegrees)]
    public Animation FieldOfView { get; } = new Animation(3, MirageSettings.MinimumFieldOfViewDegrees, MirageSettings.MaximumFieldOfViewDegrees);

    [Display(GroupName = nameof(Texts.CameraGroup), Name = nameof(Texts.CameraPitch), Description = nameof(Texts.CameraPitchDescription), Order = 21, ResourceType = typeof(Texts))]
    [AnimationSlider("F2", "°", MirageSettings.MinimumCameraPitchDegrees, MirageSettings.MaximumCameraPitchDegrees)]
    public Animation CameraPitch { get; } = new Animation(0, MirageSettings.MinimumCameraPitchDegrees, MirageSettings.MaximumCameraPitchDegrees);

    private IAnimatable[]? _animatables;

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
    {
        try
        {
            return new MirageEffectProcessor(devices, this);
        }
        catch (Exception exception)
        {
            MirageTelemetry.Report(exception);
            throw;
        }
    }

    protected override IEnumerable<IAnimatable> GetAnimatables()
        => _animatables ??= [Amount, EyeHeight, HeatDifference, HeatThickness, InversionDifference, InversionHeight, InversionThickness, Distance, FieldOfView, CameraPitch, Shimmer, ShimmerSpeed];
}
