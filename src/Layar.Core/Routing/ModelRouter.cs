using System.Text.Json;
using Laya.Core.Lang;

namespace Laya.Core.Routing;

/// <summary>
/// Decides which checkpoint a request should use, without loading or running anything. Precedence:
/// explicit model &gt; explicit task &gt; detected typed-decisions workflow (opt-in) &gt; explicit
/// lang &gt; detected script/language &gt; default.
/// </summary>
/// <remarks>
/// This is the pure decision logic ported from the Python package's <c>Router.route</c>. The
/// model-lifecycle side of that class (lazily loading, LRU eviction, preloading checkpoints) is not
/// part of Laya.Core: it depends on an <see cref="IDecisionBackend"/> actually existing to load, so
/// it belongs with the backends (Laya.Onnx / Laya.TorchSharp) rather than here.
/// </remarks>
public sealed class ModelRouter(ModelKey defaultModel = ModelKey.English, bool autoTaskDetection = false)
{
	public ModelKey Default { get; } = defaultModel;

	public bool AutoTaskDetection { get; } = autoTaskDetection;

	public RouteDecision Route(
		JsonElement state,
		IReadOnlyCollection<string>? questionIds = null,
		ModelKey? model = null,
		string? task = null,
		string? lang = null)
	{
		if (model is { } explicitModel)
			return new RouteDecision(explicitModel, $"explicit model={ModelKeys.ToKeyString(explicitModel)}", null, null);

		if (task is not null)
		{
			var normalizedTask = task.Trim().ToLowerInvariant().Replace('-', '_') == "typed_decisions"
				? "typed-decisions"
				: task;
			var key = ModelKeys.Normalise(normalizedTask);
			return new RouteDecision(key, $"explicit task={task}", null, null);
		}

		var workflow = questionIds is null ? null : ModelKeys.MatchTypedDecisionsWorkflow(questionIds);
		if (workflow is not null && AutoTaskDetection)
		{
			return new RouteDecision(
				ModelKey.TypedDecisions,
				$"question ids match the '{workflow}' typed-decisions workflow",
				null, workflow);
		}

		if (lang is not null)
		{
			var primary = lang.ToLowerInvariant().Split('-')[0];
			var key = primary is "en" or "eng" or "english" ? ModelKey.English : ModelKey.Multilingual;
			return new RouteDecision(key, $"explicit lang={lang}", null, workflow);
		}

		var detection = LanguageAnalyzer.Analyse(state);
		ModelKey resultKey;
		string reason;
		if (detection.Script == "unknown")
		{
			resultKey = Default;
			reason = $"no letters detected in state; using default ({ModelKeys.ToKeyString(Default)})";
		}
		else if (detection.Script != "latin")
		{
			resultKey = ModelKey.Multilingual;
			reason = $"non-Latin script ({detection.Script}, {detection.NonLatinFraction * 100:0}% of letters); " +
				"the English checkpoint cannot read it";
		}
		else if (!detection.IsEnglish)
		{
			resultKey = ModelKey.Multilingual;
			reason = detection.Language is not null
				? $"Latin script but language looks like '{detection.Language}', not English"
				: $"Latin script, language not identified but {detection.DiacriticRate * 100:0}% non-English " +
					"letters; not safe for the English checkpoint";
		}
		else
		{
			resultKey = ModelKey.English;
			reason = "English Latin text";
		}
		return new RouteDecision(resultKey, reason, detection, workflow);
	}
}
