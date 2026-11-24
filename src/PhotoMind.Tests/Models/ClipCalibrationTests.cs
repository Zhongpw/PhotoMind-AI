using PhotoMind.Core.Models;

namespace PhotoMind.Tests.Models;

public sealed class ClipCalibrationTests
{
    [Theory]
    [InlineData(0.0, 0.5)]
    [InlineData(5.0, 0.9933071490757153)]
    [InlineData(-5.0, 0.0066928509242848554)]
    public void Sigmoid_ReturnsExpected(double x, double expected)
    {
        Assert.Equal(expected, ClipCalibration.Sigmoid(x), 10);
    }

    [Fact]
    public void Calibrate_ZeroSlope_ReturnsNaN()
    {
        // Slope <= 0 means "calibration disabled"; callers must distinguish this from a real probability.
        Assert.True(double.IsNaN(ClipCalibration.Calibrate(0.05, 0.0, -1.0)));
        Assert.True(double.IsNaN(ClipCalibration.Calibrate(0.05, -3.0, -1.0)));
    }

    [Fact]
    public void Calibrate_PositiveSlope_MapsMarginToProbability()
    {
        // A clearly positive margin should map above 0.5; a negative margin below 0.5.
        var high = ClipCalibration.Calibrate(0.1, 40.0, -1.0); // sigmoid(40*0.1 - 1) = sigmoid(3)
        var low = ClipCalibration.Calibrate(-0.1, 40.0, -1.0); // sigmoid(-5)
        Assert.True(high > 0.9, $"high={high}");
        Assert.True(low < 0.01, $"low={low}");
    }

    [Fact]
    public void Calibrate_MonotonicInMargin()
    {
        var a = ClipCalibration.Calibrate(0.01, 10.0, 0.0);
        var b = ClipCalibration.Calibrate(0.03, 10.0, 0.0);
        var c = ClipCalibration.Calibrate(0.05, 10.0, 0.0);
        Assert.True(a < b && b < c);
    }
}
