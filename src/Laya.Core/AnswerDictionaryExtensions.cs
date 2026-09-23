namespace Laya.Core;

/// <summary>Typed accessors for a <see cref="DecisionEngine.PredictAsync"/> result, so callers
/// don't write <c>(ChoiceAnswer)answers["intent"]</c> by hand. Failures name both the question id
/// and the mismatched type, rather than a bare <see cref="InvalidCastException"/>.</summary>
public static class AnswerDictionaryExtensions
{
	public static ChoiceAnswer Choice(this IReadOnlyDictionary<string, Answer> answers, string questionId) =>
		Get<ChoiceAnswer>(answers, questionId);

	public static ScoreAnswer Score(this IReadOnlyDictionary<string, Answer> answers, string questionId) =>
		Get<ScoreAnswer>(answers, questionId);

	public static NoulAnswer Noul(this IReadOnlyDictionary<string, Answer> answers, string questionId) =>
		Get<NoulAnswer>(answers, questionId);

	private static T Get<T>(IReadOnlyDictionary<string, Answer> answers, string questionId) where T : Answer
	{
		ArgumentNullException.ThrowIfNull(answers);
		if (!answers.TryGetValue(questionId, out var answer))
		{
			throw new KeyNotFoundException(
				$"no answer for question '{questionId}' — check the id matches a key in the questions dictionary passed to PredictAsync");
		}
		if (answer is not T typed)
		{
			throw new InvalidCastException(
				$"question '{questionId}' answered as {answer.GetType().Name}, not {typeof(T).Name} " +
				$"— check the question's actual type ({answer.Type}) matches what you expected");
		}
		return typed;
	}
}
