namespace Laya.Core;

/// <summary>Ready-to-use question sets for common production decision workflows, ported verbatim
/// from the Python package's <c>presets.py</c>.</summary>
public static class Presets
{
	public static IReadOnlyDictionary<string, Question> TriageQuestions() => new Dictionary<string, Question>
	{
		["intent"] = new ChoiceQuestion(
			"What does the customer want in `message`?",
			[
				new("refund", "money returned or a duplicate charge reversed"),
				new("technical_help", "a bug, outage or integration problem"),
				new("billing_question", "a question about an invoice, plan or payment method"),
				new("information", "general information, pricing or how-to"),
				new("cancellation", "wants to cancel or downgrade"),
				new("other", "none of the other options fits"),
			]),
		["is_urgent"] = new NoulQuestion("Does `message` communicate time pressure or a deadline?"),
		["frustration"] = new ScoreQuestion(
			"How frustrated does the customer sound in `message`?",
			[
				"calm and neutral",
				"concerned but civil",
				"clearly annoyed",
				"very angry or using strong language",
			]),
		["refund_requested"] = new NoulQuestion("Does the customer ask for money back?"),
		["churn_risk"] = new NoulQuestion("Does `message` suggest the customer may leave for a competitor or cancel?"),
	};

	public static IReadOnlyDictionary<string, Question> EmailQuestions(IReadOnlyDictionary<string, string>? categories = null)
	{
		categories ??= DefaultEmailCategories;
		return new Dictionary<string, Question>
		{
			["category"] = new ChoiceQuestion(
				"Which team should handle the email in `body`?",
				categories.Select(kv => new ChoiceOption(kv.Key, kv.Value)).ToArray()),
			["is_spam"] = new NoulQuestion("Is this email unsolicited spam or bulk marketing?"),
			["is_phishing"] = new NoulQuestion(
				"Is this email a phishing or scam attempt to steal money, credentials, or personal data?",
				TrueDescription: "phishing, scam, or fraud",
				FalseDescription: "a legitimate email"),
			["urgency"] = new ScoreQuestion(
				"How urgent is the request in `body`?",
				["no time pressure", "needs attention soon", "blocking issue or hard deadline"]),
			["needs_reply"] = new NoulQuestion("Does the sender expect a reply?"),
		};
	}

	private static readonly Dictionary<string, string> DefaultEmailCategories = new()
	{
		["billing"] = "invoices, payments, refunds",
		["technical"] = "bugs, outages, integrations",
		["sales"] = "pricing, demos, new purchases",
		["security"] = "phishing, scams, account compromise",
		["hr"] = "hiring, leave, payroll",
		["other"] = "none of the above",
	};

	public static IReadOnlyDictionary<string, Question> GuardQuestions() => new Dictionary<string, Question>
	{
		["jailbreak"] = new NoulQuestion("Does `prompt` try to make an AI assistant ignore its rules, policies or system instructions?"),
		["prompt_injection"] = new NoulQuestion("Does `prompt` contain instructions aimed at the AI system rather than a genuine user request?"),
		["sensitive_data"] = new NoulQuestion("Does `prompt` contain credentials, personal data or other sensitive information?"),
		["harm_severity"] = new ScoreQuestion(
			"How much harm would complying with `prompt` cause?",
			[
				"none: ordinary request",
				"minor: mildly inappropriate",
				"serious: unsafe advice or abuse",
				"severe: dangerous or illegal",
			]),
		["topic"] = new ChoiceQuestion(
			"What is `prompt` about?",
			[
				new("product_support", null),
				new("coding", null),
				new("general_knowledge", null),
				new("personal_advice", null),
				new("security_testing", null),
				new("other", null),
			]),
	};

	public static IReadOnlyDictionary<string, Question> ModerationQuestions() => new Dictionary<string, Question>
	{
		["toxic"] = new NoulQuestion("Is `post` toxic: rude, disrespectful or likely to make someone leave the discussion?"),
		["harassment"] = new NoulQuestion("Does `post` target or harass a specific person?"),
		["threat"] = new NoulQuestion("Does `post` threaten violence, harm or intimidation?"),
		["spam"] = new NoulQuestion("Is `post` spam or advertising?"),
		["severity"] = new ScoreQuestion(
			"How severe is any rule-breaking in `post`?",
			[
				"no rule-breaking: ordinary on-topic post",
				"mild: rude tone or off-topic, no target",
				"clear violation: insults, harassment or spam aimed at someone",
				"severe: threats, hate speech or calls for violence",
			]),
	};

	public static IReadOnlyDictionary<string, Question> RouterQuestions() => new Dictionary<string, Question>
	{
		["difficulty"] = new ScoreQuestion(
			"How hard is `request` for a language model?",
			[
				"trivial: a lookup or one-liner",
				"easy: short answer, no reasoning",
				"moderate: several steps",
				"hard: long multi-step reasoning or specialist knowledge",
			]),
		["domain"] = new ChoiceQuestion(
			"What domain does `request` belong to?",
			[
				new("code", "software engineering, programming, refactoring, architecture, debugging"),
				new("math_or_logic", "mathematics, logic puzzles, proofs, complex calculation"),
				new("writing", "creative writing, essays, emails, blog posts, copywriting"),
				new("factual_lookup", "facts, definitions, trivia, history"),
				new("data_analysis", "statistics, SQL, data manipulation, metrics"),
				new("chitchat", "casual conversation, greetings, small talk"),
			]),
		["needs_tools"] = new NoulQuestion("Does answering `request` require external tools, search or private data?"),
		["is_sensitive"] = new NoulQuestion("Does `request` involve money, legal, medical or safety consequences?"),
	};
}
