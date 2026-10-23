namespace Mirage.Tests;

public sealed class MirageHorizonTests
{
    [Theory]
    [InlineData(1280.0, 0.0262, 24427.480916)]
    [InlineData(640.0, 0.5773503, 554.256258)]
    [InlineData(64.0, 0.0017453, 18334.4)]
    public void TheFocalLengthIsHalfTheWidthOverTheHalfFieldTangent(double width, double tangent, double expected)
    {
        Assert.Equal(expected, MirageHorizon.FocalLength(width, tangent), 1.0);
    }

    [Fact]
    public void WithoutAPositionThePitchIsTheTilt()
    {
        Assert.Equal(0.0123, MirageHorizon.Pitch(0.0123, 0.0, 1500.0), 1e-15);
    }

    [Theory]
    [InlineData(0.0, 150.0, 2000.0)]
    [InlineData(0.02, -300.0, 900.0)]
    [InlineData(-0.1, 40.0, 120.0)]
    public void ThePositionAddsTheAngleOfItsPixelsToTheTilt(double tilt, double position, double focal)
    {
        Assert.Equal(tilt + Math.Atan(position / focal), MirageHorizon.Pitch(tilt, position, focal), 1e-15);
    }

    [Theory]
    [InlineData(4000.0, 30.0)]
    [InlineData(-4000.0, 30.0)]
    [InlineData(1e9, 1e5)]
    public void ThePitchStopsBelowTheZenithAndTheNadir(double position, double focal)
    {
        var pitch = MirageHorizon.Pitch(0.35, position, focal);
        var opposite = MirageHorizon.Pitch(-0.35, -position, focal);

        Assert.InRange(pitch, -MirageSettings.MaximumPitchRadians, MirageSettings.MaximumPitchRadians);
        Assert.Equal(-pitch, opposite, 1e-15);
        Assert.True(Math.Abs(pitch) <= Math.PI / 2.0 - 0.08);
    }

    [Theory]
    [InlineData(0.0, 2000.0)]
    [InlineData(0.15, 700.0)]
    [InlineData(-0.2, 40.0)]
    public void TheRowIsTheFocalLengthTimesTheTangentOfThePitch(double pitch, double focal)
    {
        Assert.Equal(focal * Math.Tan(pitch), MirageHorizon.Row(pitch, focal), 1e-9);
    }

    [Theory]
    [InlineData(120.0, 2000.0)]
    [InlineData(-640.0, 900.0)]
    [InlineData(5.5, 24000.0)]
    public void WithoutATiltTheRowOfTheHorizonIsThePosition(double position, double focal)
    {
        Assert.Equal(position, MirageHorizon.Row(MirageHorizon.Pitch(0.0, position, focal), focal), 1e-9);
    }

    [Theory]
    [InlineData(0.0, 120.0, 2000.0)]
    [InlineData(0.2, -80.0, 600.0)]
    [InlineData(-0.3, 350.0, 1100.0)]
    [InlineData(0.34, 900.0, 800.0)]
    public void ThePositionOfARowIsInvertedBackToThePositionThatPlacedIt(double tilt, double position, double focal)
    {
        var row = MirageHorizon.Row(MirageHorizon.Pitch(tilt, position, focal), focal);

        Assert.Equal(position, MirageHorizon.Position(row, tilt, focal), 1e-6);
    }

    [Theory]
    [InlineData(-120.0, 2000.0, 9.0)]
    [InlineData(35.0, 800.0, -14.5)]
    public void WithoutATiltMovingTheRowMovesThePositionByTheSameAmount(double position, double focal, double move)
    {
        var row = MirageHorizon.Row(MirageHorizon.Pitch(0.0, position, focal), focal);

        Assert.Equal(position + move, MirageHorizon.Position(row + move, 0.0, focal), 1e-9);
    }
}
