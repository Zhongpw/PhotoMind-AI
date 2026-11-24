namespace PhotoMind.Core.Models;

/// <summary>
/// Logistic (Platt-style) calibration for CLIP top-1/top-2 similarity margins.
///
/// The raw temperature-softmax probability of a zero-shot CLIP model is <b>not</b> a well-calibrated
/// probability of correctness: because all "a photo of {label}" text embeddings are near-degenerate,
/// the softmax over a handful of categories stays close to uniform even when the model is confident
/// about the ordering. We instead calibrate on the <b>margin</b> between the top-1 and top-2 raw cosine
/// similarities — a more informative, threshold-independent signal — with a learned logistic map:
///
///     calibrated = sigmoid(slope * margin + intercept)
///
/// The coefficients must be fit on a held-out, human-labelled calibration set and are persisted as
/// <see cref="DTOs.AiConfig.ClipCalibrationSlope"/> / <see cref="DTOs.AiConfig.ClipCalibrationIntercept"/>.
/// When the slope is 0 the calibration is disabled and callers fall back to the raw softmax confidence.
/// </summary>
public static class ClipCalibration
{
    /// <summary>Logistic function, numerically stable.</summary>
    public static double Sigmoid(double x)
    {
        if (x >= 0)
        {
            var z = Math.Exp(-x);
            return 1.0 / (1.0 + z);
        }

        var e = Math.Exp(x);
        return e / (1.0 + e);
    }

    /// <summary>
    /// Maps a top-1/top-2 similarity margin to a calibrated probability of correctness.
    /// Returns <see cref="double.NaN"/> when the calibration is disabled (slope &lt;= 0), so callers
    /// can distinguish "not calibrated" from a genuine probability.
    /// </summary>
    public static double Calibrate(double margin, double slope, double intercept)
    {
        if (slope <= 0)
            return double.NaN;

        return Sigmoid(slope * margin + intercept);
    }
}
