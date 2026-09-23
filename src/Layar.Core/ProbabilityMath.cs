using System.Numerics.Tensors;

namespace Laya.Core;

/// <summary>
/// The calibration and probability math shared by every answer type: temperature scaling,
/// softmax, and the normalized-entropy confidence score. Operates on <see cref="Span{T}"/> so a
/// caller can reuse one small buffer across every question in a request instead of allocating per
/// answer.
/// </summary>
public static class ProbabilityMath
{
	/// <summary>
	/// A fitted temperature below this would sharpen rather than soften logits — the Python port's
	/// own English checkpoint ships one bucket at 0.1006, which turns a 24% top probability into a
	/// published 99%. No honest calibration needs to sharpen this hard, so it is rejected rather
	/// than applied.
	/// </summary>
	public const float TemperatureMin = 0.5f;

	public const float TemperatureMax = 5.0f;

	/// <summary>Confines <paramref name="temperature"/> to <see cref="TemperatureMin"/>..
	/// <see cref="TemperatureMax"/>, falling back to 1.0 for NaN/infinity.</summary>
	public static float ClampTemperature(float temperature)
	{
		if (float.IsNaN(temperature) || float.IsInfinity(temperature))
			return 1.0f;
		return Math.Clamp(temperature, TemperatureMin, TemperatureMax);
	}

	/// <summary>The calibration bucket a question falls into: its type plus a coarse option-count
	/// band. Matches the bucket keys a fitted checkpoint's <c>temperature_by_options</c> ships.</summary>
	public static string TemperatureBucket(QuestionType type, int optionCount)
	{
		var band = optionCount switch
		{
			<= 2 => "2",
			<= 5 => "3-5",
			<= 10 => "6-10",
			_ => "11+",
		};
		var typeName = type switch
		{
			QuestionType.Choice => "choice",
			QuestionType.Score => "score",
			QuestionType.Noul => "noul",
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
		};
		return $"{typeName}:{band}";
	}

	/// <summary>
	/// Temperature-scaled softmax over the first <paramref name="optionCount"/> logits: divides by
	/// <paramref name="temperature"/>, subtracts the max for numerical stability, then normalizes.
	/// <paramref name="destination"/> may alias <paramref name="logits"/>.
	/// </summary>
	public static void SoftmaxWithTemperature(
		ReadOnlySpan<float> logits,
		int optionCount,
		float temperature,
		Span<float> destination)
	{
		var z = logits[..optionCount];
		var dest = destination[..optionCount];
		if (temperature != 1.0f)
			TensorPrimitives.Divide(z, temperature, dest);
		else
			z.CopyTo(dest);

		// Subtract the max before exponentiating: the decision head's action logits routinely run
		// into the hundreds or thousands, and TensorPrimitives.SoftMax's own exp() overflows to
		// +Infinity for inputs that large, turning Infinity/Infinity into NaN.
		var max = TensorPrimitives.Max(dest);
		TensorPrimitives.Subtract(dest, max, dest);
		TensorPrimitives.SoftMax(dest, dest);
	}

	/// <summary>
	/// Normalized Shannon entropy confidence: <c>1 - H(p) / log(k)</c>, clipped to [0, 1].
	/// A single-option question (k &lt; 2) is fully confident by construction.
	/// </summary>
	public static float ConfidenceFromProbabilities(ReadOnlySpan<float> probabilities)
	{
		var k = probabilities.Length;
		if (k < 2)
			return 1.0f;

		var entropy = 0.0f;
		foreach (var p in probabilities)
		{
			var clamped = Math.Clamp(p, 1e-12f, 1.0f);
			entropy -= clamped * MathF.Log(clamped);
		}
		return Math.Clamp(1.0f - (entropy / MathF.Log(k)), 0.0f, 1.0f);
	}

	/// <summary>Expected Calibration Error across confidence bins: the weighted average gap
	/// between reported confidence and observed accuracy within each bin.</summary>
	public static float EceScore(ReadOnlySpan<float> confidence, ReadOnlySpan<bool> correct, int bins = 15)
	{
		if (confidence.Length == 0)
			return float.NaN;
		if (confidence.Length != correct.Length)
			throw new ArgumentException("confidence and correct must have the same length", nameof(correct));

		var n = confidence.Length;
		var error = 0.0f;
		for (var b = 0; b < bins; b++)
		{
			var lo = (float)b / bins;
			var hi = (float)(b + 1) / bins;

			var count = 0;
			var confSum = 0.0f;
			var correctSum = 0.0f;
			for (var i = 0; i < n; i++)
			{
				if (confidence[i] <= lo || confidence[i] > hi)
					continue;
				count++;
				confSum += confidence[i];
				correctSum += correct[i] ? 1.0f : 0.0f;
			}
			if (count == 0)
				continue;

			var binWeight = (float)count / n;
			var gap = MathF.Abs((confSum / count) - (correctSum / count));
			error += binWeight * gap;
		}
		return error;
	}
}
