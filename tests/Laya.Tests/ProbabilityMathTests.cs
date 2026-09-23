using AwesomeAssertions;
using Laya.Core;

namespace Laya.Tests;

public class ProbabilityMathTests
{
	[Test]
	public async Task SoftmaxWithTemperature_normalizes_to_one()
	{
		float[] logits = [2f, 1f, 0.1f];
		var dest = new float[3];
		ProbabilityMath.SoftmaxWithTemperature(logits, 3, 1f, dest);
		dest.Sum().Should().BeApproximately(1f, 1e-5f);
		dest[0].Should().BeGreaterThan(dest[1]);
		dest[1].Should().BeGreaterThan(dest[2]);
	}

	[Test]
	public async Task SoftmaxWithTemperature_handles_extreme_magnitude_logits_without_nan()
	{
		float[] logits = [1203.17f, -1437.54f];
		var dest = new float[2];
		ProbabilityMath.SoftmaxWithTemperature(logits, 2, 1f, dest);
		dest[0].Should().BeApproximately(1f, 1e-6f);
		dest[1].Should().BeApproximately(0f, 1e-6f);
		float.IsNaN(dest[0]).Should().BeFalse();
	}

	[Test]
	public async Task ConfidenceFromProbabilities_is_one_for_certain_answer()
	{
		float[] probs = [1f, 0f, 0f, 0f];
		ProbabilityMath.ConfidenceFromProbabilities(probs).Should().BeApproximately(1f, 1e-4f);
	}

	[Test]
	public async Task ConfidenceFromProbabilities_is_zero_for_uniform_answer()
	{
		float[] probs = [0.25f, 0.25f, 0.25f, 0.25f];
		ProbabilityMath.ConfidenceFromProbabilities(probs).Should().BeApproximately(0f, 1e-4f);
	}

	[Test]
	public async Task ConfidenceFromProbabilities_single_option_is_fully_confident()
	{
		ProbabilityMath.ConfidenceFromProbabilities([1f]).Should().Be(1f);
	}

	[Test]
	[Arguments(0.1f, 0.5f)]
	[Arguments(10f, 5f)]
	[Arguments(1f, 1f)]
	[Arguments(float.NaN, 1f)]
	public async Task ClampTemperature_confines_to_valid_range(float input, float expected)
	{
		ProbabilityMath.ClampTemperature(input).Should().Be(expected);
	}

	[Test]
	[Arguments(QuestionType.Choice, 2, "choice:2")]
	[Arguments(QuestionType.Choice, 4, "choice:3-5")]
	[Arguments(QuestionType.Score, 8, "score:6-10")]
	[Arguments(QuestionType.Noul, 20, "noul:11+")]
	public async Task TemperatureBucket_matches_python_bucket_names(QuestionType type, int optionCount, string expected)
	{
		ProbabilityMath.TemperatureBucket(type, optionCount).Should().Be(expected);
	}
}
