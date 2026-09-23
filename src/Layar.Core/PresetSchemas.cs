namespace Laya.Core;

/// <summary>Typed counterpart to <see cref="Presets.TriageQuestions"/> + <see cref="PresetResults.AsTriage"/>.</summary>
public sealed class TriageSchema : IQuestionSchema<TriageResult>
{
	/// <summary>The question set never varies, so one instance is shared rather than rebuilt per call.</summary>
	public static readonly TriageSchema Instance = new();

	public IReadOnlyDictionary<string, Question> Questions { get; } = Presets.TriageQuestions();

	public TriageResult Bind(IReadOnlyDictionary<string, Answer> answers) => answers.AsTriage();
}

/// <summary>Typed counterpart to <see cref="Presets.EmailQuestions"/> + <see cref="PresetResults.AsEmailTriage"/>.
/// Unlike the other preset schemas, categories are caller-supplied, so this is constructed per
/// configuration rather than shared as a singleton.</summary>
public sealed class EmailTriageSchema(IReadOnlyDictionary<string, string>? categories = null) : IQuestionSchema<EmailTriageResult>
{
	public IReadOnlyDictionary<string, Question> Questions { get; } = Presets.EmailQuestions(categories);

	public EmailTriageResult Bind(IReadOnlyDictionary<string, Answer> answers) => answers.AsEmailTriage();
}

/// <summary>Typed counterpart to <see cref="Presets.GuardQuestions"/> + <see cref="PresetResults.AsGuard"/>.</summary>
public sealed class GuardSchema : IQuestionSchema<GuardResult>
{
	public static readonly GuardSchema Instance = new();

	public IReadOnlyDictionary<string, Question> Questions { get; } = Presets.GuardQuestions();

	public GuardResult Bind(IReadOnlyDictionary<string, Answer> answers) => answers.AsGuard();
}

/// <summary>Typed counterpart to <see cref="Presets.ModerationQuestions"/> + <see cref="PresetResults.AsModeration"/>.</summary>
public sealed class ModerationSchema : IQuestionSchema<ModerationResult>
{
	public static readonly ModerationSchema Instance = new();

	public IReadOnlyDictionary<string, Question> Questions { get; } = Presets.ModerationQuestions();

	public ModerationResult Bind(IReadOnlyDictionary<string, Answer> answers) => answers.AsModeration();
}

/// <summary>Typed counterpart to <see cref="Presets.RouterQuestions"/> + <see cref="PresetResults.AsModelRouting"/>.</summary>
public sealed class ModelRouterSchema : IQuestionSchema<ModelRouterResult>
{
	public static readonly ModelRouterSchema Instance = new();

	public IReadOnlyDictionary<string, Question> Questions { get; } = Presets.RouterQuestions();

	public ModelRouterResult Bind(IReadOnlyDictionary<string, Answer> answers) => answers.AsModelRouting();
}
