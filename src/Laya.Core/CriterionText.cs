using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Laya.Core;

/// <summary>
/// Renders an arbitrary criterion value the way the Python package's <c>render_criterion</c> does:
/// a string passes through unchanged; anything else (object, array, number, bool, null) becomes
/// compact JSON with a space after every <c>,</c> and <c>:</c>, matching
/// <c>json.dumps(value, ensure_ascii=False, separators=(", ", ": "))</c> exactly — so a criterion
/// ported from an existing Python question schema renders as the same text the model was
/// calibrated against. Built on <see cref="JsonElement"/> rather than the reflection-based
/// <c>JsonSerializer.Serialize&lt;T&gt;</c>, so this stays AOT/trim-safe with no source-generated
/// context required.
/// </summary>
public static class CriterionText
{
	/// <summary>Renders <paramref name="value"/>. A JSON string renders as its raw text (no
	/// quotes); anything else renders as JSON.</summary>
	public static string Render(JsonElement value)
	{
		if (value.ValueKind == JsonValueKind.String)
			return value.GetString() ?? "";

		var sb = new StringBuilder();
		Write(value, sb);
		return sb.ToString();
	}

	/// <summary>Like <see cref="Render"/>, but returns null for a missing/null/empty-string value
	/// — the "no description, render the bare label" case <see cref="ChoiceOption"/> and
	/// <see cref="NoulQuestion"/> already treat specially.</summary>
	public static string? RenderOrNull(JsonElement? value)
	{
		if (value is not { } v || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
			return null;
		if (v.ValueKind == JsonValueKind.String && v.GetString() is null or "")
			return null;
		return Render(v);
	}

	private static void Write(JsonElement value, StringBuilder sb)
	{
		switch (value.ValueKind)
		{
			case JsonValueKind.Object:
				_ = sb.Append('{');
				var firstProperty = true;
				foreach (var property in value.EnumerateObject())
				{
					if (!firstProperty)
						_ = sb.Append(", ");
					firstProperty = false;
					WriteJsonString(property.Name, sb);
					_ = sb.Append(": ");
					Write(property.Value, sb);
				}
				_ = sb.Append('}');
				break;

			case JsonValueKind.Array:
				_ = sb.Append('[');
				var firstItem = true;
				foreach (var item in value.EnumerateArray())
				{
					if (!firstItem)
						_ = sb.Append(", ");
					firstItem = false;
					Write(item, sb);
				}
				_ = sb.Append(']');
				break;

			case JsonValueKind.String:
				WriteJsonString(value.GetString() ?? "", sb);
				break;

			case JsonValueKind.Number:
				_ = sb.Append(value.GetRawText());
				break;

			case JsonValueKind.True:
				_ = sb.Append("true");
				break;

			case JsonValueKind.False:
				_ = sb.Append("false");
				break;

			case JsonValueKind.Null:
			case JsonValueKind.Undefined:
				_ = sb.Append("null");
				break;
		}
	}

	private static void WriteJsonString(string text, StringBuilder sb)
	{
		_ = sb.Append('"');
		foreach (var ch in text)
		{
			var escaped = ch switch
			{
				'"' => "\\\"",
				'\\' => "\\\\",
				'\n' => "\\n",
				'\r' => "\\r",
				'\t' => "\\t",
				< (char)0x20 => "\\u" + ((int)ch).ToString("x4", CultureInfo.InvariantCulture),
				_ => null,
			};
			_ = escaped is null ? sb.Append(ch) : sb.Append(escaped);
		}
		_ = sb.Append('"');
	}
}
