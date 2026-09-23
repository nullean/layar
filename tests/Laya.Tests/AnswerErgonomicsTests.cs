using AwesomeAssertions;
using Laya.Core;

namespace Laya.Tests;

public class AnswerErgonomicsTests
{
	private static readonly ActionInfo NoAction = new(1f);

	[Test]
	public async Task Choice_Score_Noul_return_the_typed_answer_without_a_cast()
	{
		var answers = new Dictionary<string, Answer>
		{
			["intent"] = new ChoiceAnswer("refund", new Dictionary<string, float> { ["refund"] = 1f }, 1f, NoAction),
			["frustration"] = new ScoreAnswer(2f, ["a", "b", "c"], [0f, 0f, 1f], 1f, NoAction),
			["churn_risk"] = new NoulAnswer(0.2f, 0.8f, NoAction),
		};

		answers.Choice("intent").Choice.Should().Be("refund");
		answers.Score("frustration").Score.Should().Be(2f);
		answers.Noul("churn_risk").Noul.Should().Be(0.2f);
	}

	[Test]
	public async Task Missing_key_throws_a_clear_KeyNotFoundException()
	{
		var answers = new Dictionary<string, Answer>();
		var act = () => answers.Choice("missing");
		act.Should().Throw<KeyNotFoundException>().WithMessage("*missing*");
	}

	[Test]
	public async Task Wrong_type_throws_a_clear_InvalidCastException_naming_both_types()
	{
		var answers = new Dictionary<string, Answer>
		{
			["churn_risk"] = new NoulAnswer(0.2f, 0.8f, NoAction),
		};
		var act = () => answers.Choice("churn_risk");
		act.Should().Throw<InvalidCastException>().WithMessage("*churn_risk*NoulAnswer*ChoiceAnswer*");
	}

	[Test]
	public async Task AsTriage_binds_the_full_typed_result_from_a_raw_answer_dictionary()
	{
		var answers = new Dictionary<string, Answer>
		{
			["intent"] = new ChoiceAnswer("refund", new Dictionary<string, float> { ["refund"] = 1f }, 1f, NoAction),
			["is_urgent"] = new NoulAnswer(0.9f, 0.9f, NoAction),
			["frustration"] = new ScoreAnswer(2f, ["a", "b", "c"], [0f, 0f, 1f], 1f, NoAction),
			["refund_requested"] = new NoulAnswer(0.8f, 0.8f, NoAction),
			["churn_risk"] = new NoulAnswer(0.3f, 0.7f, NoAction),
		};

		var triage = answers.AsTriage();

		triage.Intent.Choice.Should().Be("refund");
		triage.IsUrgent.Noul.Should().Be(0.9f);
		triage.Frustration.Score.Should().Be(2f);
		triage.RefundRequested.Noul.Should().Be(0.8f);
		triage.ChurnRisk.Noul.Should().Be(0.3f);
	}
}
