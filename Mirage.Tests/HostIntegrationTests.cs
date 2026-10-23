using System.Windows;
using Telemetry;

namespace Mirage.Tests;

public sealed class HostIntegrationTests
{
    [Fact]
    public void OutsideAWpfApplicationNoTelemetryIsStartedOrSent()
    {
        Assert.Null(Application.Current);

        MirageTelemetry.EnsureStartedOnce();
        MirageTelemetry.Report(new InvalidOperationException());

        Assert.Null(ProcessState.Read("DrainClaimed"));
        Assert.Null(ProcessState.Read("SentCount"));
    }

    [Fact]
    public void OutsideAWpfApplicationTheEffectCanStillBeCreated()
    {
        Assert.Null(Application.Current);

        var effect = new MirageEffect();

        Assert.Equal(Texts.Mirage, effect.Label);
        Assert.Null(ProcessState.Read("DrainClaimed"));
    }
}
