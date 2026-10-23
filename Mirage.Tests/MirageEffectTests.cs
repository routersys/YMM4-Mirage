using System.ComponentModel.DataAnnotations;
using System.Reflection;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace Mirage.Tests;

public sealed class MirageEffectTests
{
    static PropertyInfo Property(string name) => typeof(MirageEffect).GetProperty(name)!;

    static T Attribute<T>(string property) where T : Attribute => Property(property).GetCustomAttribute<T>()!;

    static Animation[] Animations(MirageEffect effect)
        => [effect.Amount, effect.EyeHeight, effect.HeatDifference, effect.HeatThickness, effect.InversionDifference, effect.InversionHeight, effect.InversionThickness, effect.Distance, effect.FieldOfView, effect.CameraPitch];

    [Theory]
    [InlineData(nameof(MirageEffect.Amount), 100d, 0d, 100d)]
    [InlineData(nameof(MirageEffect.EyeHeight), 2d, 0.5d, 20d)]
    [InlineData(nameof(MirageEffect.HeatDifference), 25d, 5d, 60d)]
    [InlineData(nameof(MirageEffect.HeatThickness), 0.5d, 0.1d, 3d)]
    [InlineData(nameof(MirageEffect.InversionDifference), 12d, 1d, 20d)]
    [InlineData(nameof(MirageEffect.InversionHeight), 60d, 10d, 300d)]
    [InlineData(nameof(MirageEffect.InversionThickness), 10d, 2d, 50d)]
    [InlineData(nameof(MirageEffect.Distance), 40d, 5d, 100d)]
    [InlineData(nameof(MirageEffect.FieldOfView), 3d, 0.2d, 60d)]
    [InlineData(nameof(MirageEffect.CameraPitch), 0d, -20d, 20d)]
    public void AnimatedParametersStartFromTheirDefaultsWithinTheirRange(string name, double defaultValue, double minimum, double maximum)
    {
        var effect = new MirageEffect();

        var animation = (Animation)Property(name).GetValue(effect)!;

        Assert.Equal(defaultValue, animation.DefaultValue);
        Assert.Equal(minimum, animation.MinValue);
        Assert.Equal(maximum, animation.MaxValue);
        Assert.Equal(defaultValue, animation.GetValue(0, 1, EffectDescriptions.Fps));
    }

    [Fact]
    public void TheRangesMatchTheLimitsTheModelCovers()
    {
        var effect = new MirageEffect();

        Assert.Equal(MirageSettings.MinimumEyeHeightMeters, effect.EyeHeight.MinValue);
        Assert.Equal(MirageSettings.MaximumEyeHeightMeters, effect.EyeHeight.MaxValue);
        Assert.Equal(MirageSettings.MinimumHeatDifference, effect.HeatDifference.MinValue);
        Assert.Equal(MirageSettings.MaximumHeatDifference, effect.HeatDifference.MaxValue);
        Assert.Equal(MirageSettings.MinimumHeatThicknessMeters, effect.HeatThickness.MinValue);
        Assert.Equal(MirageSettings.MaximumHeatThicknessMeters, effect.HeatThickness.MaxValue);
        Assert.Equal(MirageSettings.MinimumInversionDifference, effect.InversionDifference.MinValue);
        Assert.Equal(MirageSettings.MaximumInversionDifference, effect.InversionDifference.MaxValue);
        Assert.Equal(MirageSettings.MinimumInversionHeightMeters, effect.InversionHeight.MinValue);
        Assert.Equal(MirageSettings.MaximumInversionHeightMeters, effect.InversionHeight.MaxValue);
        Assert.Equal(MirageSettings.MinimumInversionThicknessMeters, effect.InversionThickness.MinValue);
        Assert.Equal(MirageSettings.MaximumInversionThicknessMeters, effect.InversionThickness.MaxValue);
        Assert.Equal(MirageSettings.MinimumDistanceKilometers, effect.Distance.MinValue);
        Assert.Equal(MirageSettings.MaximumDistanceKilometers, effect.Distance.MaxValue);
        Assert.Equal(MirageSettings.MinimumFieldOfViewDegrees, effect.FieldOfView.MinValue);
        Assert.Equal(MirageSettings.MaximumFieldOfViewDegrees, effect.FieldOfView.MaxValue);
        Assert.Equal(MirageSettings.MinimumCameraPitchDegrees, effect.CameraPitch.MinValue);
        Assert.Equal(MirageSettings.MaximumCameraPitchDegrees, effect.CameraPitch.MaxValue);
    }

    [Fact]
    public void TheDefaultKindIsInferiorAndTheDefaultSourceIsThePhoto()
    {
        var effect = new MirageEffect();

        Assert.Equal(MirageKind.Inferior, effect.Kind);
        Assert.Equal(MirageSource.Photo, effect.Source);
    }

    [Fact]
    public void TheAnimatedParametersAreListedOnceAndReused()
    {
        var effect = new MirageEffect();
        var method = typeof(MirageEffect).GetMethod("GetAnimatables", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var first = (IEnumerable<IAnimatable>)method.Invoke(effect, null)!;
        var second = (IEnumerable<IAnimatable>)method.Invoke(effect, null)!;

        Assert.Same(first, second);
        Assert.Equal(Animations(effect), first);
    }

    [Fact]
    public void TheLabelIsTheLocalizedEffectName()
    {
        Assert.Equal(Texts.Mirage, new MirageEffect().Label);
    }

    [Fact]
    public void TheTenNumericParametersReceiveTheAnimationParameters()
    {
        var effect = new MirageEffect();

        effect.SetAnimationParameters(120, EffectDescriptions.Fps);

        Assert.Equal(10, Animations(effect).Length);
        Assert.All(Animations(effect), animation => Assert.Equal(120, animation.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void NoExoFilterIsWrittenForAviUtl(int keyFrameIndex)
    {
        var effect = new MirageEffect();

        var description = new ExoOutputDescription(new VideoInfo(), string.Empty, new AviUtlDirectories(string.Empty, string.Empty));

        Assert.Empty(effect.CreateExoVideoFilters(keyFrameIndex, description));
    }

    [Fact]
    public void TheAuthorIsDeclared()
    {
        var details = typeof(MirageEffect).GetCustomAttribute<PluginDetailsAttribute>()!;

        Assert.Equal("routersys", details.AuthorName);
    }

    [Fact]
    public void TheEffectIsRegisteredForFilteringWithoutAviUtlSupport()
    {
        var attribute = typeof(MirageEffect).GetCustomAttribute<VideoEffectAttribute>()!;

        Assert.Equal(nameof(Texts.Mirage), attribute.Name);
        Assert.Equal([VideoEffectCategories.Filtering], attribute.Categories);
        Assert.Equal([nameof(Texts.TagMirage), nameof(Texts.TagRefraction), nameof(Texts.TagAtmosphere)], attribute.Keywords);
        Assert.False(attribute.IsAviUtlSupported);
        Assert.True(attribute.IsEffectItemSupported);
        Assert.Equal(typeof(Texts), attribute.ResourceType);
        Assert.Equal(Texts.Mirage, attribute.GetName());
    }

    [Theory]
    [InlineData(nameof(MirageEffect.Amount), nameof(Texts.BasicGroup), nameof(Texts.Amount), nameof(Texts.AmountDescription), 0)]
    [InlineData(nameof(MirageEffect.Kind), nameof(Texts.AtmosphereGroup), nameof(Texts.Kind), nameof(Texts.KindDescription), 10)]
    [InlineData(nameof(MirageEffect.EyeHeight), nameof(Texts.AtmosphereGroup), nameof(Texts.EyeHeight), nameof(Texts.EyeHeightDescription), 11)]
    [InlineData(nameof(MirageEffect.HeatDifference), nameof(Texts.AtmosphereGroup), nameof(Texts.HeatDifference), nameof(Texts.HeatDifferenceDescription), 12)]
    [InlineData(nameof(MirageEffect.HeatThickness), nameof(Texts.AtmosphereGroup), nameof(Texts.HeatThickness), nameof(Texts.HeatThicknessDescription), 13)]
    [InlineData(nameof(MirageEffect.InversionDifference), nameof(Texts.AtmosphereGroup), nameof(Texts.InversionDifference), nameof(Texts.InversionDifferenceDescription), 14)]
    [InlineData(nameof(MirageEffect.InversionHeight), nameof(Texts.AtmosphereGroup), nameof(Texts.InversionHeight), nameof(Texts.InversionHeightDescription), 15)]
    [InlineData(nameof(MirageEffect.InversionThickness), nameof(Texts.AtmosphereGroup), nameof(Texts.InversionThickness), nameof(Texts.InversionThicknessDescription), 16)]
    [InlineData(nameof(MirageEffect.Distance), nameof(Texts.AtmosphereGroup), nameof(Texts.Distance), nameof(Texts.DistanceDescription), 17)]
    [InlineData(nameof(MirageEffect.Source), nameof(Texts.AtmosphereGroup), nameof(Texts.Source), nameof(Texts.SourceDescription), 18)]
    [InlineData(nameof(MirageEffect.FieldOfView), nameof(Texts.CameraGroup), nameof(Texts.FieldOfView), nameof(Texts.FieldOfViewDescription), 20)]
    [InlineData(nameof(MirageEffect.CameraPitch), nameof(Texts.CameraGroup), nameof(Texts.CameraPitch), nameof(Texts.CameraPitchDescription), 21)]
    public void EveryParameterIsDisplayedInItsGroupInOrder(string property, string group, string name, string description, int order)
    {
        var display = Attribute<DisplayAttribute>(property);

        Assert.Equal(group, display.GroupName);
        Assert.Equal(name, display.Name);
        Assert.Equal(description, display.Description);
        Assert.Equal(order, display.Order);
        Assert.Equal(typeof(Texts), display.ResourceType);
    }

    [Theory]
    [InlineData(nameof(MirageEffect.Amount), "F1", "%", 0d, 100d)]
    [InlineData(nameof(MirageEffect.EyeHeight), "F1", "m", 0.5d, 20d)]
    [InlineData(nameof(MirageEffect.HeatDifference), "F1", "K", 5d, 60d)]
    [InlineData(nameof(MirageEffect.HeatThickness), "F2", "m", 0.1d, 3d)]
    [InlineData(nameof(MirageEffect.InversionDifference), "F1", "K", 1d, 20d)]
    [InlineData(nameof(MirageEffect.InversionHeight), "F1", "m", 10d, 300d)]
    [InlineData(nameof(MirageEffect.InversionThickness), "F1", "m", 2d, 50d)]
    [InlineData(nameof(MirageEffect.Distance), "F1", "km", 5d, 100d)]
    [InlineData(nameof(MirageEffect.FieldOfView), "F2", "°", 0.2d, 60d)]
    [InlineData(nameof(MirageEffect.CameraPitch), "F2", "°", -20d, 20d)]
    public void AnimatedParametersAreEditedWithAnimationSliders(string property, string format, string unit, double minimum, double maximum)
    {
        var slider = Attribute<AnimationSliderAttribute>(property);

        Assert.Equal(format, slider.StringFormat);
        Assert.Equal(unit, slider.UnitText);
        Assert.Equal(minimum, slider.DefaultMin);
        Assert.Equal(maximum, slider.DefaultMax);
    }

    [Theory]
    [InlineData(nameof(MirageEffect.HeatDifference), MirageKind.Inferior)]
    [InlineData(nameof(MirageEffect.HeatThickness), MirageKind.Inferior)]
    [InlineData(nameof(MirageEffect.InversionDifference), MirageKind.Superior)]
    [InlineData(nameof(MirageEffect.InversionHeight), MirageKind.Superior)]
    [InlineData(nameof(MirageEffect.InversionThickness), MirageKind.Superior)]
    [InlineData(nameof(MirageEffect.Distance), MirageKind.Superior)]
    [InlineData(nameof(MirageEffect.Source), MirageKind.Superior)]
    public void TheParametersOfOneKindAreShownOnlyForThatKind(string property, MirageKind kind)
    {
        var visibility = Attribute<ShowPropertyEditorWhenAttribute>(property);

        Assert.Equal(nameof(MirageEffect.Kind), visibility.PropertyName);
        Assert.Equal(kind, visibility.Value);
    }

    [Theory]
    [InlineData(nameof(MirageEffect.Amount))]
    [InlineData(nameof(MirageEffect.Kind))]
    [InlineData(nameof(MirageEffect.EyeHeight))]
    [InlineData(nameof(MirageEffect.FieldOfView))]
    [InlineData(nameof(MirageEffect.CameraPitch))]
    public void TheCommonParametersAreAlwaysShown(string property)
    {
        Assert.Null(Property(property).GetCustomAttribute<ShowPropertyEditorWhenAttribute>());
    }

    [Fact]
    public void EverySettingSurvivesAProjectRoundTrip()
    {
        var effect = new MirageEffect { Kind = MirageKind.Superior, Source = MirageSource.Scene };
        var values = new[] { 55d, 7d, 31d, 1.25d, 9d, 120d, 14d, 63d, 2.5d, -3.5d };
        foreach (var (animation, value) in Animations(effect).Zip(values))
            animation.Values[0].Value = value;

        var clone = Json.GetClone(effect)!;

        Assert.NotSame(effect, clone);
        Assert.Equal(MirageKind.Superior, clone.Kind);
        Assert.Equal(MirageSource.Scene, clone.Source);
        Assert.Equal(values, Animations(clone).Select(animation => animation.GetValue(0, 1, EffectDescriptions.Fps)));
    }
}
