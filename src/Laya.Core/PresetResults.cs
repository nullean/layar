namespace Laya.Core;

/// <summary>
/// Strongly-typed results for each <see cref="Presets"/> question set — no string keys, no casts.
/// Only possible because these schemas are fixed at compile time; an arbitrary caller-defined
/// question dictionary has no equivalent (there's nothing to generate a typed wrapper from) and
/// stays accessed via <see cref="AnswerDictionaryExtensions"/> instead.
/// </summary>
public static class PresetResults
{
	public static TriageResult AsTriage(this IReadOnlyDictionary<string, Answer> answers) => new(
		Intent: answers.Choice("intent"),
		IsUrgent: answers.Noul("is_urgent"),
		Frustration: answers.Score("frustration"),
		RefundRequested: answers.Noul("refund_requested"),
		ChurnRisk: answers.Noul("churn_risk"));

	public static EmailTriageResult AsEmailTriage(this IReadOnlyDictionary<string, Answer> answers) => new(
		Category: answers.Choice("category"),
		IsSpam: answers.Noul("is_spam"),
		IsPhishing: answers.Noul("is_phishing"),
		Urgency: answers.Score("urgency"),
		NeedsReply: answers.Noul("needs_reply"));

	public static GuardResult AsGuard(this IReadOnlyDictionary<string, Answer> answers) => new(
		Jailbreak: answers.Noul("jailbreak"),
		PromptInjection: answers.Noul("prompt_injection"),
		SensitiveData: answers.Noul("sensitive_data"),
		HarmSeverity: answers.Score("harm_severity"),
		Topic: answers.Choice("topic"));

	public static ModerationResult AsModeration(this IReadOnlyDictionary<string, Answer> answers) => new(
		Toxic: answers.Noul("toxic"),
		Harassment: answers.Noul("harassment"),
		Threat: answers.Noul("threat"),
		Spam: answers.Noul("spam"),
		Severity: answers.Score("severity"));

	public static ModelRouterResult AsModelRouting(this IReadOnlyDictionary<string, Answer> answers) => new(
		Difficulty: answers.Score("difficulty"),
		Domain: answers.Choice("domain"),
		NeedsTools: answers.Noul("needs_tools"),
		IsSensitive: answers.Noul("is_sensitive"));
}

/// <summary>Typed result of <see cref="Presets.TriageQuestions"/>.</summary>
public sealed record TriageResult(ChoiceAnswer Intent, NoulAnswer IsUrgent, ScoreAnswer Frustration, NoulAnswer RefundRequested, NoulAnswer ChurnRisk);

/// <summary>Typed result of <see cref="Presets.EmailQuestions"/>.</summary>
public sealed record EmailTriageResult(ChoiceAnswer Category, NoulAnswer IsSpam, NoulAnswer IsPhishing, ScoreAnswer Urgency, NoulAnswer NeedsReply);

/// <summary>Typed result of <see cref="Presets.GuardQuestions"/>.</summary>
public sealed record GuardResult(NoulAnswer Jailbreak, NoulAnswer PromptInjection, NoulAnswer SensitiveData, ScoreAnswer HarmSeverity, ChoiceAnswer Topic);

/// <summary>Typed result of <see cref="Presets.ModerationQuestions"/>.</summary>
public sealed record ModerationResult(NoulAnswer Toxic, NoulAnswer Harassment, NoulAnswer Threat, NoulAnswer Spam, ScoreAnswer Severity);

/// <summary>Typed result of <see cref="Presets.RouterQuestions"/>.</summary>
public sealed record ModelRouterResult(ScoreAnswer Difficulty, ChoiceAnswer Domain, NoulAnswer NeedsTools, NoulAnswer IsSensitive);
