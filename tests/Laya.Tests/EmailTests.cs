using AwesomeAssertions;
using Laya.Core;

namespace Laya.Tests;

public class EmailTests
{
	[Test]
	public async Task CleanBody_strips_quoted_reply_history()
	{
		var body = "Please refund my charge.\n\nOn Tue, Jan 1, 2026 at 3:00 PM John wrote:\n> original message\n> more quoted text";
		var cleaned = Email.CleanBody(body);
		cleaned.Should().Contain("Please refund my charge.");
		cleaned.Should().NotContain("original message");
	}

	[Test]
	public async Task CleanBody_strips_signature_block()
	{
		var body = "Please refund my charge as soon as possible, this is urgent for our team.\n\nBest regards,\nJohn Smith\nSenior Manager";
		var cleaned = Email.CleanBody(body);
		cleaned.Should().Contain("Please refund my charge");
		cleaned.Should().NotContain("Senior Manager");
	}

	[Test]
	public async Task CleanBody_strips_confidentiality_disclaimer()
	{
		var body = "Please process this refund today. This email is confidential and intended solely for the addressee.";
		var cleaned = Email.CleanBody(body);
		cleaned.Should().Contain("Please process this refund today.");
		cleaned.Should().NotContain("confidential");
	}

	[Test]
	public async Task BuildState_trims_subject_and_cleans_body()
	{
		var state = Email.BuildState("  Refund request  ", "Please help.\n\n--\nJohn");
		state.Subject.Should().Be("Refund request");
		state.Body.Should().Be("Please help.");
	}
}
