namespace Laya.Core;

/// <summary>
/// A fixed, named question set with a compile-time-typed result — the strongly-typed counterpart
/// to a caller-built <see cref="IReadOnlyDictionary{TKey, TValue}"/> of <see cref="Question"/>.
/// Both drive the same <see cref="DecisionEngine.PredictAsync(string, IReadOnlyDictionary{string, Question}, CancellationToken)"/>
/// forward pass; a schema also knows how to turn the answers back into a record, so a caller never
/// touches a question id as a string. A composed, runtime-defined question set (for example
/// <see cref="Presets.EmailQuestions"/> called with caller-supplied categories that vary per
/// tenant) has no fixed shape to generate a record from, so it stays on the dictionary path and is
/// read back via <see cref="AnswerDictionaryExtensions"/> instead — the two are complementary, not
/// a replacement for one another.
/// </summary>
/// <typeparam name="TResult">The record type an answered instance binds to.</typeparam>
public interface IQuestionSchema<out TResult>
{
	/// <summary>The fixed question set this schema answers.</summary>
	IReadOnlyDictionary<string, Question> Questions { get; }

	/// <summary>Turns one <see cref="DecisionEngine.PredictAsync(string, IReadOnlyDictionary{string, Question}, CancellationToken)"/>
	/// result into <typeparamref name="TResult"/>.</summary>
	TResult Bind(IReadOnlyDictionary<string, Answer> answers);
}

/// <summary>Typed <see cref="DecisionEngine.PredictAsync(string, IReadOnlyDictionary{string, Question}, CancellationToken)"/>
/// overload for an <see cref="IQuestionSchema{TResult}"/>.</summary>
public static class QuestionSchemaExtensions
{
	public static async ValueTask<TResult> PredictAsync<TResult>(
		this DecisionEngine engine,
		string state,
		IQuestionSchema<TResult> schema,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(engine);
		ArgumentNullException.ThrowIfNull(schema);
		var answers = await engine.PredictAsync(state, schema.Questions, cancellationToken).ConfigureAwait(false);
		return schema.Bind(answers);
	}
}
