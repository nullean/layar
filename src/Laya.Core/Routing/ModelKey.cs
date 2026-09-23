using System.Collections.Frozen;

namespace Laya.Core.Routing;

/// <summary>The three checkpoints a <see cref="ModelRouter"/> chooses between.</summary>
public enum ModelKey
{
	English,
	Multilingual,
	TypedDecisions,
}

public static class ModelKeys
{
	private static readonly FrozenDictionary<string, ModelKey> ByName = new Dictionary<string, ModelKey>
	{
		["english"] = ModelKey.English,
		["multilingual"] = ModelKey.Multilingual,
		["typed-decisions"] = ModelKey.TypedDecisions,
		// aliases people are likely to type
		["en"] = ModelKey.English,
		["laya"] = ModelKey.English,
		["default"] = ModelKey.English,
		["multi"] = ModelKey.Multilingual,
		["ml"] = ModelKey.Multilingual,
		["laya-multilingual"] = ModelKey.Multilingual,
		["typed"] = ModelKey.TypedDecisions,
		["typed_decisions"] = ModelKey.TypedDecisions,
		["laya-typed-decisions"] = ModelKey.TypedDecisions,
		["decisions"] = ModelKey.TypedDecisions,
	}.ToFrozenDictionary();

	/// <summary>Fixed signatures of the typed-decisions workflows the Python port ships. Matched by
	/// exact question-id set, never a subset, so an unrelated schema that happens to contain
	/// "urgency" is never captured.</summary>
	private static readonly FrozenDictionary<string, FrozenSet<string>> TypedDecisionWorkflows =
		new Dictionary<string, string[]>
		{
			["agent_trace_observability"] = ["action", "needs_review", "outcome", "risk", "urgency"],
			["customer_service"] = ["action", "category", "churn_risk", "needs_human", "urgency"],
			["invoice_processing"] = ["discrepancy_severity", "disposition", "duplicate", "matches_order", "urgency"],
			["security_incidents"] = ["credential_compromise", "disposition", "severity", "true_positive", "urgency"],
		}.ToFrozenDictionary(kv => kv.Key, kv => kv.Value.ToFrozenSet());

	public static ModelKey Normalise(string name)
	{
		ArgumentNullException.ThrowIfNull(name);
		var key = name.Trim().ToLowerInvariant();
		if (ByName.TryGetValue(key, out var model))
			return model;
		throw new ArgumentException(
			$"unknown model '{name}'; choose one of english, multilingual, typed-decisions (or an alias)",
			nameof(name));
	}

	public static string ToKeyString(ModelKey key) => key switch
	{
		ModelKey.English => "english",
		ModelKey.Multilingual => "multilingual",
		ModelKey.TypedDecisions => "typed-decisions",
		_ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
	};

	/// <summary>The name of the typed-decisions workflow whose question ids exactly match
	/// <paramref name="questionIds"/>, or null.</summary>
	public static string? MatchTypedDecisionsWorkflow(IReadOnlyCollection<string> questionIds)
	{
		var ids = questionIds.ToFrozenSet();
		foreach (var (workflow, signature) in TypedDecisionWorkflows)
		{
			if (ids.SetEquals(signature))
				return workflow;
		}
		return null;
	}
}
