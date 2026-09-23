namespace Laya.Core;

/// <summary>
/// The decision head's action-probability side channel: how confident the model is that its
/// top answer needs no further review. Not the same number as <see cref="Answer.Confidence"/>,
/// which is derived from the answer's own probability distribution.
/// </summary>
public readonly record struct ActionInfo(float ActProbability);

public abstract record Answer(QuestionType Type, float Confidence, ActionInfo Action);

public sealed record ChoiceAnswer(
	string Choice,
	IReadOnlyDictionary<string, float> Probabilities,
	float Confidence,
	ActionInfo Action) : Answer(QuestionType.Choice, Confidence, Action);

public sealed record ScoreAnswer(
	float Score,
	IReadOnlyList<string> Legend,
	IReadOnlyList<float> Probabilities,
	float Confidence,
	ActionInfo Action) : Answer(QuestionType.Score, Confidence, Action);

/// <summary>Noul: a calibrated P(true) from 0.0 to 1.0. Confidence is max(p, 1-p).</summary>
public sealed record NoulAnswer(float Noul, float Confidence, ActionInfo Action)
	: Answer(QuestionType.Noul, Confidence, Action);
