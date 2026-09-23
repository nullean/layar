using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Laya.Core;

/// <summary>State built by <see cref="Email.BuildState"/>, ready to serialize into the text a
/// <see cref="SequenceBuilder"/> packs a question around.</summary>
public sealed record EmailState(string Subject, string Body, string? From, IReadOnlyDictionary<string, string>? Extra)
{
	/// <summary>JSON-serializes this state using <see cref="JsonObject"/> (no reflection-based
	/// serializer, so this stays reflection-free under trimming/NativeAOT).</summary>
	public string ToStateText()
	{
		var obj = new JsonObject
		{
			["subject"] = Subject,
			["body"] = Body,
		};
		if (From is not null)
			obj["from"] = From;
		if (Extra is not null)
		{
			foreach (var (key, value) in Extra)
				obj[key] = value;
		}
		return obj.ToJsonString();
	}
}

/// <summary>Cleans email bodies before they reach a decision model: strips quoted history,
/// signatures and legal disclaimers so the model sees the sender's actual request rather than
/// boilerplate. Ported from the Python package's <c>email.py</c>.</summary>
public static partial class Email
{
	[GeneratedRegex(@"^\s*On .{0,300}wrote:\s*$", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
	private static partial Regex QuoteHeaderOnWrote();

	[GeneratedRegex(@"^\s*-{2,}\s*(Original|Forwarded) Message\s*-{2,}", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
	private static partial Regex QuoteHeaderOriginalForwarded();

	[GeneratedRegex(@"^\s*_{8,}\s*$", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
	private static partial Regex QuoteHeaderUnderscores();

	[GeneratedRegex(@"^\s*From:\s.+$", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
	private static partial Regex QuoteHeaderFrom();

	[GeneratedRegex(@"^\s*--\s*$", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
	private static partial Regex SignatureDashes();

	[GeneratedRegex(@"^\s*(best|kind|warm|many thanks|thanks|thank you|regards|cheers|sincerely)[\w ,!.]*$",
		RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
	private static partial Regex SignatureClosing();

	[GeneratedRegex(@"^\s*sent from my (iphone|android|mobile|ipad)", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
	private static partial Regex SignatureSentFrom();

	[GeneratedRegex(
		"(confidential|intended (solely )?for the (use of the )?(named )?(addressee|recipient)|" +
		"if you (have )?received this (e-?mail|message) in error)",
		RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 2000)]
	private static partial Regex Disclaimer();

	[GeneratedRegex(@"(?<=[.!?])\s+", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
	private static partial Regex SentenceBoundary();

	[GeneratedRegex(@"\n\s*\n", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
	private static partial Regex ParagraphBoundary();

	[GeneratedRegex(@"[ \t]+", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
	private static partial Regex RunsOfSpaces();

	private static bool IsQuoteHeader(string line) =>
		QuoteHeaderOnWrote().IsMatch(line) ||
		QuoteHeaderOriginalForwarded().IsMatch(line) ||
		QuoteHeaderUnderscores().IsMatch(line) ||
		QuoteHeaderFrom().IsMatch(line);

	private static bool IsSignatureMarker(string line) =>
		SignatureDashes().IsMatch(line) ||
		SignatureClosing().IsMatch(line) ||
		SignatureSentFrom().IsMatch(line);

	private static string StripDisclaimer(string paragraph)
	{
		if (!Disclaimer().IsMatch(paragraph))
			return paragraph;

		var parts = SentenceBoundary().Split(paragraph)
			.Select(p => p.Trim())
			.Where(p => p.Length > 0);
		var kept = parts.Where(p => !Disclaimer().IsMatch(p));
		return string.Join(' ', kept);
	}

	/// <summary>Removes quoted email history, signatures and disclaimers so a downstream question
	/// sees the sender's actual request rather than boilerplate.</summary>
	public static string CleanBody(string? body, int maxChars = 3000)
	{
		var text = (body ?? string.Empty)
			.Replace("\r\n", "\n")
			.Replace("\r", "\n")
			.Replace("\\n", "\n");

		var rawLines = text.Split('\n');
		var lines = new List<string>();
		foreach (var raw in rawLines)
		{
			if (IsQuoteHeader(raw) && lines.Count > 0)
				break;
			if (raw.TrimStart().StartsWith('>'))
				continue;
			lines.Add(raw.TrimEnd());
		}

		var cut = lines.Count;
		var start = Math.Max(1, Math.Min((int)(lines.Count * 0.6), lines.Count - 8));
		for (var i = start; i < lines.Count; i++)
		{
			if (lines[i].Trim().Length <= 40 && IsSignatureMarker(lines[i]))
			{
				cut = i;
				break;
			}
		}
		lines = lines[..cut];

		var joined = string.Join('\n', lines);
		var paragraphs = ParagraphBoundary().Split(joined)
			.Select(StripDisclaimer)
			.Select(p => p.Trim())
			.Where(p => p.Length > 0);

		var result = RunsOfSpaces().Replace(string.Join("\n\n", paragraphs), " ");
		return result.Length <= maxChars ? result : result[..maxChars];
	}

	/// <summary>Builds a clean state for email classification: trims the subject and, by default,
	/// runs <paramref name="body"/> through <see cref="CleanBody"/>.</summary>
	public static EmailState BuildState(
		string? subject,
		string? body,
		string? sender = null,
		bool clean = true,
		IReadOnlyDictionary<string, string>? extra = null) =>
		new(
			(subject ?? string.Empty).Trim(),
			clean ? CleanBody(body) : body ?? string.Empty,
			sender,
			extra);
}
