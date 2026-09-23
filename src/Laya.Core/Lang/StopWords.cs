using System.Buffers;
using System.Collections.Frozen;

namespace Laya.Core.Lang;

/// <summary>
/// Function-word lists used to name a Latin-script language, and the non-English diacritic set
/// used when no list matches. Both are static, long-lived lookup tables, so they are built once as
/// <see cref="FrozenSet{T}"/> / <see cref="SearchValues{T}"/> rather than re-hashed per call.
/// </summary>
internal static class StopWords
{
	/// <summary>
	/// Latin-script languages overlap heavily (de/la/le/un/e/que), so each hit is weighted and a
	/// margin is required before calling something non-English — see
	/// <see cref="LatinLanguageDetector"/>. The Romance lists deliberately carry unaccented function
	/// words too: a state that lost its accents (mail clients, ticket systems, ASCII-normalizing
	/// pipelines) keeps no diacritic signal, so `la`, `un`, `y`, `e`, `et` and friends are the only
	/// evidence left.
	/// </summary>
	public static readonly FrozenDictionary<string, FrozenSet<string>> ByLanguage = new Dictionary<string, string[]>
	{
		["en"] =
		[
			"the", "and", "is", "are", "was", "were", "to", "of", "in", "for", "with", "that",
			"this", "it", "you", "have", "has", "not", "but", "on", "at", "be", "as", "from",
			"will", "can", "would", "there", "their", "what", "which", "please", "we", "i",
		],
		["fr"] =
		[
			"le", "la", "les", "des", "une", "est", "pour", "dans", "que", "qui", "avec", "sur",
			"pas", "plus", "nous", "vous", "être", "cette", "mais", "sont", "ont", "aux", "ce",
			"et", "du", "au", "ou", "je", "tu", "il", "elle", "ils", "elles", "mon", "ton",
			"ma", "ta", "sa", "mes", "tes", "ses", "ces", "deux", "trois", "très", "bien",
			"tout", "tous", "toute", "fait", "veux", "veut", "peux", "peut", "dois", "doit",
			"merci", "bonjour", "jour", "jours", "mois", "fois", "quand", "comment", "pourquoi",
			"alors", "donc",
		],
		["de"] =
		[
			"der", "die", "das", "und", "ist", "ein", "eine", "den", "dem", "nicht", "mit", "für",
			"auf", "von", "zu", "sich", "auch", "werden", "wurde", "haben", "sind", "oder", "aber",
		],
		["es"] =
		[
			"el", "los", "las", "que", "por", "con", "para", "una", "es", "se", "del", "como",
			"pero", "son", "está", "este", "esta", "todo", "más", "muy", "hay", "sus",
			// `de`/`en` are Spanish too, but they are common English tokens as well (`de facto`,
			// `en-US`, `en route`, `Rio de Janeiro`), and a state of those alone already carries no
			// English function word for the margin to weigh them against, so they stay out.
			"la", "un", "y", "al", "lo", "le", "les", "su", "mi", "tu", "nos",
			"ni", "dos", "tres", "fue", "fueron", "ser", "tiene", "tienen", "tengo", "puede",
			"pueden", "quiero", "necesito", "hemos", "han", "sobre", "entre", "cuando", "donde",
			"porque", "aunque", "también", "ya", "eso", "esto", "esa", "ese", "nada", "algo",
			"aquí", "hoy", "gracias",
		],
		["pt"] =
		[
			"os", "as", "que", "em", "um", "uma", "para", "com", "não", "é", "se", "do", "da",
			"dos", "das", "mas", "são", "está", "este", "esta", "muito", "pelo", "pela",
			// `no` is Portuguese too, and among its most frequent words; it is also one of the most
			// frequent English words, so it stays out and short Portuguese states that lean on it
			// alone are left to the diacritic rate.
			"o", "e", "na", "nas", "nos", "ao", "aos", "por", "foi", "era", "ser", "sou",
			"tem", "tenho", "pode", "podem", "quero", "preciso", "eu", "meu", "minha", "seu",
			"sua", "isso", "isto", "aqui", "ali", "como", "quando", "onde", "porque", "mais",
			"já", "ainda", "agora", "hoje", "ontem", "dois", "três", "tudo", "nada", "obrigado",
			"olá",
		],
		["it"] =
		[
			"il", "lo", "gli", "che", "di", "per", "con", "non", "è", "si", "del", "della", "sono",
			"questo", "questa", "anche", "come", "più", "nella", "alla",
			"la", "le", "un", "uno", "una", "e", "ed", "o", "da", "su", "tra", "fra", "mi",
			"ci", "ne", "ho", "hai", "ha", "abbiamo", "avete", "hanno", "era", "stato", "stata",
			"devo", "deve", "devono", "voglio", "vorrei", "mio", "mia", "tuo", "sua", "quando",
			"dove", "perche", "molto", "poco", "sempre", "mai", "già", "ancora", "adesso", "oggi",
			"ieri", "grazie", "ciao", "scusa",
			// the articulated prepositions: Italian-only words, which lets a state made of shared
			// articles (`la fattura`) still name the language rather than stay undecided.
			"nel", "nell", "negli", "sul", "sulla", "sulle", "dal", "dalla", "dallo", "dagli", "dei",
			"delle", "dello", "degli", "agli", "alle", "col",
		],
		["nl"] =
		[
			"het", "een", "van", "is", "op", "te", "dat", "niet", "met", "voor", "zijn", "aan",
			"door", "maar", "ook", "worden", "deze", "naar", "wordt",
		],
		// Romanian words its Romance neighbours do not share, so adding `ro` cannot steal a
		// French/Spanish/Italian/Portuguese state: `la`, `o`, `un`, `de`, `pe`, `ca` are
		// deliberately left out for that reason, and the diacritic signal carries the rest.
		["ro"] =
		[
			"și", "să", "este", "sunt", "care", "pentru", "din", "dar", "după", "până", "fără",
			"ale", "lui", "în", "fost", "acum", "vreau", "trebuie", "foarte", "acest", "această",
			"acesta", "aceasta", "mi", "ți", "vă", "nu",
		],
	}.ToFrozenDictionary(kv => kv.Key, kv => kv.Value.ToFrozenSet());

	/// <summary>Words more than one language list claims. Matching one of these says "not English"
	/// without saying which language, so <see cref="LatinLanguageDetector"/> only names a language
	/// that also matched at least one word no other list claims.</summary>
	public static readonly FrozenSet<string> Shared = ByLanguage.Values
		.SelectMany(set => set)
		.GroupBy(w => w)
		.Where(g => ByLanguage.Values.Count(set => set.Contains(g.Key)) > 1)
		.Select(g => g.Key)
		.ToFrozenSet();

	/// <summary>Letters ordinary English does not use — the signal that catches a Latin-script
	/// language with no stopword list at all (Polish, Czech, Turkish, Baltic, ...).</summary>
	public static readonly SearchValues<char> NonEnglishDiacritics = SearchValues.Create(
		"àâäãáåçéèêëíìîïñóòôöõøúùûüýÿßæœ" + // Western European
		"ăâîșțşţ" +                          // Romanian
		"ąćęłńśźż" +                         // Polish
		"čďěňřšťůž" +                        // Czech / Slovak
		"őű" +                               // Hungarian
		"ğı" +                               // Turkish (text is lowercased before matching)
		"āēģīķļņūž" +                        // Baltic
		"đ");                                // Serbo-Croatian / Vietnamese
}
