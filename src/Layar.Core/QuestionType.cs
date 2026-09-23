namespace Laya.Core;

/// <summary>
/// The three typed-decision primitives a <see cref="Question"/> can be. The numeric values match
/// the decision head's <c>qtype</c> embedding index, so they must not be renumbered.
/// </summary>
public enum QuestionType
{
	Choice = 0,
	Score = 1,
	Noul = 2,
}
