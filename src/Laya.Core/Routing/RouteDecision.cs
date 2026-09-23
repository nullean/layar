using Laya.Core.Lang;

namespace Laya.Core.Routing;

/// <summary>The routing outcome: which checkpoint, why, and what was detected (when detection ran
/// at all — an explicit <c>model</c>/<c>task</c>/<c>lang</c> skips it).</summary>
public readonly record struct RouteDecision(
	ModelKey Model,
	string Reason,
	LanguageAnalysis? Detection,
	string? Workflow);
