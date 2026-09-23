namespace Laya.Tokenization;

/// <summary>The core byte-pair-encoding merge loop shared by both tokenizer families: repeatedly
/// merge the lowest-rank adjacent symbol pair until none of the remaining pairs has a rank.</summary>
internal static class BpeMerger
{
	public static List<string> Merge(IReadOnlyList<string> initialSymbols, IReadOnlyDictionary<(string Left, string Right), int> ranks)
	{
		var symbols = new List<string>(initialSymbols);
		if (symbols.Count < 2)
			return symbols;

		while (true)
		{
			var bestRank = int.MaxValue;
			var bestIndex = -1;
			for (var i = 0; i < symbols.Count - 1; i++)
			{
				if (ranks.TryGetValue((symbols[i], symbols[i + 1]), out var rank) && rank < bestRank)
				{
					bestRank = rank;
					bestIndex = i;
				}
			}
			if (bestIndex < 0)
				break;

			symbols[bestIndex] += symbols[bestIndex + 1];
			symbols.RemoveAt(bestIndex + 1);
		}
		return symbols;
	}
}
