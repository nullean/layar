using System.Text.Json;

namespace Laya.Core.Routing;

/// <summary>Typed <see cref="RouterEngine.PredictAsync(JsonElement, IReadOnlyDictionary{string, Question}, ModelKey?, string?, string?, CancellationToken)"/>
/// overload for an <see cref="IQuestionSchema{TResult}"/>.</summary>
public static class RouterEngineQuestionSchemaExtensions
{
	public static async ValueTask<TResult> PredictAsync<TResult>(
		this RouterEngine router,
		JsonElement state,
		IQuestionSchema<TResult> schema,
		ModelKey? model = null,
		string? task = null,
		string? lang = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(router);
		ArgumentNullException.ThrowIfNull(schema);
		var answers = await router.PredictAsync(state, schema.Questions, model, task, lang, cancellationToken).ConfigureAwait(false);
		return schema.Bind(answers);
	}
}
