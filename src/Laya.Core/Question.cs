namespace Laya.Core;

/// <summary>
/// One labelled option of a <see cref="ChoiceQuestion"/>. <paramref name="Description"/> is
/// rendered as <c>"{Label}: {Description}"</c>; a null or empty description renders as the bare
/// label, matching the Python port's "only None/empty means no description" rule (a `false`-valued
/// criterion is still a description, just not a string one — this port only accepts strings).
/// </summary>
public readonly record struct ChoiceOption(string Label, string? Description);

/// <summary>
/// A typed question to evaluate over some state, in one of the three decision primitives.
/// </summary>
public abstract record Question(string Instructions)
{
	public abstract QuestionType Type { get; }

	/// <summary>
	/// Option texts in label-index order, exactly as sent to the sequence builder. Noul is always
	/// <c>[false, true]</c> — index 1 is the "true" probability the decision head reports.
	/// </summary>
	public abstract IReadOnlyList<string> RenderOptions();
}

public sealed record ChoiceQuestion(string Instructions, IReadOnlyList<ChoiceOption> Options) : Question(Instructions)
{
	public override QuestionType Type => QuestionType.Choice;

	public override IReadOnlyList<string> RenderOptions()
	{
		var rendered = new string[Options.Count];
		for (var i = 0; i < Options.Count; i++)
		{
			var option = Options[i];
			rendered[i] = string.IsNullOrEmpty(option.Description)
				? option.Label
				: $"{option.Label}: {option.Description}";
		}
		return rendered;
	}
}

public sealed record ScoreQuestion(string Instructions, IReadOnlyList<string> Levels) : Question(Instructions)
{
	public override QuestionType Type => QuestionType.Score;

	public override IReadOnlyList<string> RenderOptions()
	{
		var rendered = new string[Levels.Count];
		for (var i = 0; i < Levels.Count; i++)
			rendered[i] = $"level {i}: {Levels[i]}";
		return rendered;
	}
}

public sealed record NoulQuestion(string Instructions, string? TrueDescription = null, string? FalseDescription = null)
	: Question(Instructions)
{
	public override QuestionType Type => QuestionType.Noul;

	public override IReadOnlyList<string> RenderOptions() =>
	[
		"false: " + (string.IsNullOrEmpty(FalseDescription) ? "no, the statement does not hold" : FalseDescription),
		"true: " + (string.IsNullOrEmpty(TrueDescription) ? "yes, the statement holds" : TrueDescription),
	];
}
