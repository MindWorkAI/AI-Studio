using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Removes the personal details it recognizes from a session of the BeyondFacts Lab before the person shares it: links,
/// e-mail addresses, paths, phone numbers, and the names and terms the person lists.
/// </summary>
/// <remarks>
/// <para>
/// The anonymizer follows rules and asks no model, so it recognizes only what its rules describe. Names count only when
/// the person lists them; the lab suggests the ones it knows, see BflShareNames. A name is replaced in full, with its
/// titles, in the genitive, with umlauts written out or left out, and in the forms of web addresses, such as
/// "anna-mueller"; its first and its last name are replaced on their own as well, but only as they are written with a
/// capital letter, since many names are common words. A part which several people share becomes a placeholder
/// without a number.
/// </para>
/// <para>
/// Running the anonymizer again over its own result changes nothing: it leaves its placeholders alone, and the same
/// lists give the same numbers. That is why the person may edit the result and have it anonymized once more.
/// </para>
/// </remarks>
public static partial class BflAnonymizer
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflAnonymizer).Namespace, nameof(BflAnonymizer));

    /// <summary>
    /// The fewest digits a phone number has; a shorter number may be anything.
    /// </summary>
    private const int MIN_PHONE_DIGITS = 7;

    /// <summary>
    /// The fewest letters a part of a name needs to be replaced on its own, so that an initial stays.
    /// </summary>
    private const int MIN_PART_LETTERS = 2;

    /// <summary>
    /// The titles which may stand before a name, longer ones first.
    /// </summary>
    private const string TITLE = @"(?:Professorin|Professor|Prof|Dr\.?-Ing|Dipl\.?-Ing|Dr|PD|Herrn|Herr|Frau|Mrs|Mr|Ms|Mx|Sir|Dame)";

    /// <summary>
    /// Any number of titles before a name, each followed by a period or a space, e.g., "Prof. Dr. ".
    /// </summary>
    private const string TITLES = $@"(?:{TITLE}(?:\.[ \t]*|[ \t]+))*";

    /// <summary>
    /// What may stand between the parts of a name: spaces, or the characters with which web addresses join words.
    /// </summary>
    private const string PART_SEPARATOR = @"(?:\s+|[._-])?";

    /// <summary>
    /// Where a name or a term may begin: not inside a word.
    /// </summary>
    private const string BEFORE = @"(?<![\p{L}\p{M}\p{N}_])";

    /// <summary>
    /// Where a name or a term may end: not inside a word.
    /// </summary>
    private const string AFTER = @"(?![\p{L}\p{M}\p{N}_])";

    /// <summary>
    /// Where a part of a name on its own may begin: neither inside a word nor after the hyphen of a double name.
    /// </summary>
    private const string BEFORE_PART = @"(?<![\p{L}\p{M}\p{N}_])(?<!\p{L}[-‐‑])";

    /// <summary>
    /// Where a part of a name on its own may end: neither inside a word nor before the hyphen of a double name.
    /// </summary>
    private const string AFTER_PART = @"(?![\p{L}\p{M}\p{N}_]|[-‐‑]\p{L})";

    /// <summary>
    /// The small words with which many last names begin, e.g., "von der Leyen".
    /// </summary>
    internal static readonly IReadOnlySet<string> NAME_PARTICLES = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "von", "vom", "van", "de", "der", "den", "del", "della", "di", "da", "du", "la", "le", "zu", "zum", "zur", "ten", "ter",
    };

    /// <summary>
    /// Replaces the personal details the anonymizer recognizes.
    /// </summary>
    /// <param name="text">The text, e.g., a session of the lab in Markdown.</param>
    /// <param name="people">The names of people to replace, one person each, e.g., "Prof. Dr. Anna Müller".</param>
    /// <param name="terms">Further terms to replace, e.g., an institution or a place.</param>
    /// <returns>The text with placeholders, and what was replaced.</returns>
    public static BflAnonymization Anonymize(string text, IReadOnlyList<string> people, IReadOnlyList<string> terms)
    {
        var report = new List<BflReplacement>();
        var link = Placeholder(TB("Link"));
        var email = Placeholder(TB("E-mail address"));
        var path = Placeholder(TB("Path"));
        var phone = Placeholder(TB("Phone number"));
        var result = text.Normalize(NormalizationForm.FormC);

        // Links first, since an address may hold an e-mail address, a path, or a name:
        result = MarkdownLinkPattern().Replace(result, match =>
        {
            Record(report, link, match.Groups["url"].Value);
            var label = match.Groups["label"].Value.Trim();
            return label.Length > 0 ? $"{label} {link}" : link;
        });

        result = AutoLinkPattern().Replace(result, match => Record(report, link, match.Groups["url"].Value));
        result = UrlPattern().Replace(result, match => ReplaceWithoutEnd(report, link, match.Value));
        result = EmailPattern().Replace(result, match => Record(report, email, match.Value));
        result = PathPattern().Replace(result, match => ReplaceWithoutEnd(report, path, match.Value));
        result = PhonePattern().Replace(result, match => IsPhoneNumber(match.Value) ? Record(report, phone, match.Value) : match.Value);
        result = ReplaceNamesAndTerms(result, people, terms, report, [link, email, path, phone]);
        return new BflAnonymization(result, report);
    }

    /// <summary>
    /// Gets the parts of a name as the person listed it: without titles and remarks in parentheses, and in the order
    /// first name, last name, also when the person wrote "Müller, Anna".
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The parts, empty when nothing like a name remains.</returns>
    internal static IReadOnlyList<string> PartsOfName(string name)
    {
        var cleaned = ParenthesesPattern().Replace(name.Normalize(NormalizationForm.FormC), " ").Replace("**", " ").Trim().Trim(',', ';', ':', '-', '–', '*', '_').Trim();
        var commaParts = cleaned.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (commaParts.Length > 1)
        {
            // "Müller, Anna" names the last name first; "Anna Müller, TUM" adds a remark:
            cleaned = commaParts.Length is 2 && !commaParts[0].Contains(' ') && !commaParts[1].Contains(' ') ? $"{commaParts[1]} {commaParts[0]}" : commaParts[0];
        }

        var parts = cleaned.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(part => part.Trim(',', ';', ':')).Where(part => part.Any(char.IsLetter)).ToList();
        while (parts.Count > 0 && TitlePattern().IsMatch(parts[0]))
            parts.RemoveAt(0);

        return parts;
    }

    /// <summary>
    /// Tells whether a word is a title which may stand before a name, e.g., "Prof." or "Dr.".
    /// </summary>
    /// <param name="word">The word.</param>
    internal static bool IsTitle(string word) => TitlePattern().IsMatch(word);

    /// <summary>
    /// Replaces the names of people and the further terms.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="people">The names of people, one person each.</param>
    /// <param name="terms">The further terms.</param>
    /// <param name="report">What was replaced so far.</param>
    /// <param name="fixedPlaceholders">The placeholders of links, e-mail addresses, paths, and phone numbers, which stay as they are.</param>
    /// <returns>The text with placeholders.</returns>
    private static string ReplaceNamesAndTerms(string text, IReadOnlyList<string> people, IReadOnlyList<string> terms, List<BflReplacement> report, IReadOnlyList<string> fixedPlaceholders)
    {
        var personLabel = TB("Person");
        var termLabel = TB("Term");
        var patterns = new List<BflNamePattern>();
        var singleParts = new Dictionary<string, string>(StringComparer.Ordinal);
        var ambiguous = Placeholder(personLabel);

        var knownPeople = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var number = 0;
        foreach (var name in people)
        {
            var parts = PartsOfName(name);
            if (parts.Count is 0 || !knownPeople.Add(string.Join(' ', parts)))
                continue;

            var placeholder = Placeholder($"{personLabel} {++number}");
            if (parts.Count is 1)
            {
                AddSinglePart(singleParts, parts[0], placeholder, ambiguous);
                continue;
            }

            var first = parts[0];
            var lastName = LastNameOf(parts);
            patterns.Add(new(TITLES + string.Join(PART_SEPARATOR, parts.Select(PartPattern)), string.Join(' ', parts).Length, placeholder, true));
            patterns.Add(new(PhrasePattern(lastName) + @"(?:,\s*|\s+|[._-])?" + PartPattern(first), lastName.Length + first.Length + 1, placeholder, true));
            if (parts.Count > 2)
                patterns.Add(new(TITLES + PartPattern(first) + PART_SEPARATOR + PhrasePattern(lastName), first.Length + lastName.Length + 1, placeholder, true));

            AddSinglePart(singleParts, first, placeholder, ambiguous);
            AddSinglePart(singleParts, lastName, placeholder, ambiguous);
            if (lastName.Contains(' '))
                AddSinglePart(singleParts, parts[^1], placeholder, ambiguous);
        }

        foreach (var (part, placeholder) in singleParts)
            patterns.Add(new(TITLES + PhrasePattern(part), part.Length, placeholder, false));

        var knownTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        number = 0;
        foreach (var term in terms)
        {
            var parts = term.Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Where(part => part.Any(char.IsLetterOrDigit)).ToList();
            if (parts.Count is 0 || !knownTerms.Add(string.Join(' ', parts)))
                continue;

            patterns.Add(new(string.Join(PART_SEPARATOR, parts.Select(PartPattern)), string.Join(' ', parts).Length, Placeholder($"{termLabel} {++number}"), true));
        }

        if (patterns.Count is 0)
            return text;

        // The longest names first, so that "Anna Schmidt-Weber" is not taken for "Anna Schmidt":
        patterns = [.. patterns.OrderByDescending(pattern => pattern.Length)];
        var expression = new StringBuilder();
        expression.Append("(?<keep>");
        foreach (var placeholder in fixedPlaceholders)
            expression.Append(Regex.Escape(placeholder)).Append('|');

        expression.Append(Regex.Escape($"[{personLabel}")).Append(@"(?: \d+)?\]|").Append(Regex.Escape($"[{termLabel}")).Append(@"(?: \d+)?\])");
        for (var index = 0; index < patterns.Count; index++)
        {
            //
            // A part of a name on its own must not be a part of a double name, e.g., "Schmidt" in "Müller-Schmidt",
            // which may be somebody else. A full name or a term may stand in a longer word with a hyphen, e.g., in
            // the address "anna-mueller-lab":
            //
            var pattern = patterns[index];
            var groupIndex = index.ToString(CultureInfo.InvariantCulture);
            expression.Append('|').Append(pattern.IgnoresCase ? BEFORE : BEFORE_PART);
            expression.Append("(?<v").Append(groupIndex).Append('>').Append(pattern.IgnoresCase ? "(?i:" : "(?:").Append(pattern.Pattern).Append("))");
            expression.Append("(?<s").Append(groupIndex).Append(">['’]s|['’]|s)?");
            expression.Append(pattern.IgnoresCase ? AFTER : AFTER_PART);
        }

        var regex = new Regex(expression.ToString(), RegexOptions.CultureInvariant);
        var nameGroups = Enumerable.Range(0, patterns.Count).Select(index => regex.GroupNumberFromName($"v{index.ToString(CultureInfo.InvariantCulture)}")).ToArray();
        var suffixGroups = Enumerable.Range(0, patterns.Count).Select(index => regex.GroupNumberFromName($"s{index.ToString(CultureInfo.InvariantCulture)}")).ToArray();
        return regex.Replace(text, match =>
        {
            if (match.Groups["keep"].Success)
                return match.Value;

            for (var index = 0; index < patterns.Count; index++)
            {
                // The genitive stays after the placeholder, e.g., "[Person 1]s":
                var group = match.Groups[nameGroups[index]];
                if (group.Success)
                    return Record(report, patterns[index].Placeholder, group.Value) + match.Groups[suffixGroups[index]].Value;
            }

            return match.Value;
        });
    }

    /// <summary>
    /// Gets the last name of a name with several parts: from its first particle on, e.g., "von der Leyen", otherwise
    /// its last part.
    /// </summary>
    /// <param name="parts">The parts of the name, at least two.</param>
    private static string LastNameOf(IReadOnlyList<string> parts)
    {
        for (var index = 1; index < parts.Count - 1; index++)
        {
            if (NAME_PARTICLES.Contains(parts[index]))
                return string.Join(' ', parts.Skip(index));
        }

        return parts[^1];
    }

    /// <summary>
    /// Adds a part of a name which the anonymizer replaces on its own; a part several people share gets the
    /// placeholder without a number.
    /// </summary>
    /// <param name="singleParts">The parts so far, with their placeholders.</param>
    /// <param name="part">The part, e.g., a first name.</param>
    /// <param name="placeholder">The placeholder of the person.</param>
    /// <param name="ambiguous">The placeholder without a number.</param>
    private static void AddSinglePart(Dictionary<string, string> singleParts, string part, string placeholder, string ambiguous)
    {
        if (part.Count(char.IsLetter) < MIN_PART_LETTERS || NAME_PARTICLES.Contains(part))
            return;

        singleParts[part] = singleParts.TryGetValue(part, out var known) && known != placeholder ? ambiguous : placeholder;
    }

    /// <summary>
    /// Gets the pattern of several parts of a name which stand one after the other, e.g., "von der Leyen".
    /// </summary>
    /// <param name="phrase">The parts, separated by spaces.</param>
    private static string PhrasePattern(string phrase) => string.Join(@"\s+", phrase.Split(' ').Select(PartPattern));

    /// <summary>
    /// Gets the pattern of one part of a name: umlauts match their spellings with and without them, letters with
    /// accents also without them, and the period of an initial may be missing.
    /// </summary>
    /// <param name="part">The part of the name.</param>
    private static string PartPattern(string part)
    {
        var pattern = new StringBuilder();
        for (var index = 0; index < part.Length; index++)
        {
            var character = part[index];
            var next = index + 1 < part.Length ? part[index + 1] : '\0';
            if (next is 'e' or 'E' && character is 'a' or 'o' or 'u' or 'A' or 'O' or 'U')
            {
                // A name written as "Mueller" also stands for "Müller":
                pattern.Append("(?:").Append(Regex.Escape(part.Substring(index, 2))).Append('|').Append(UmlautOf(character)).Append(')');
                index++;
                continue;
            }

            if (character is 's' && next is 's')
            {
                pattern.Append("(?:ss|ß)");
                index++;
                continue;
            }

            pattern.Append(character switch
            {
                'ä' => "(?:ä|ae|a)",
                'ö' => "(?:ö|oe|o)",
                'ü' => "(?:ü|ue|u)",
                'Ä' => "(?:Ä|Ae|AE|A)",
                'Ö' => "(?:Ö|Oe|OE|O)",
                'Ü' => "(?:Ü|Ue|UE|U)",
                'ß' => "(?:ß|ss)",
                // A double name may lose its hyphen in an address, e.g., "MuellerLuedenscheidt":
                '-' or '‐' or '‑' => "[-‐‑._]?",
                '\'' or '’' => "['’]",
                '.' => @"\.?",

                _ => LetterPattern(character),
            });
        }

        return pattern.ToString();
    }

    /// <summary>
    /// Gets the umlaut of a vowel, e.g., "ü" for "u".
    /// </summary>
    /// <param name="vowel">The vowel a, o, or u, in either case.</param>
    private static string UmlautOf(char vowel) => vowel switch
    {
        'a' => "ä",
        'o' => "ö",
        'u' => "ü",
        'A' => "Ä",
        'O' => "Ö",

        _ => "Ü",
    };

    /// <summary>
    /// Gets the pattern of a letter, which matches a letter with an accent also without it.
    /// </summary>
    /// <param name="character">The letter.</param>
    private static string LetterPattern(char character)
    {
        var decomposed = character.ToString().Normalize(NormalizationForm.FormD);
        if (decomposed.Length > 1 && decomposed.Skip(1).All(mark => CharUnicodeInfo.GetUnicodeCategory(mark) is UnicodeCategory.NonSpacingMark))
            return $"(?:{Regex.Escape(character.ToString())}|{Regex.Escape(decomposed[..1])})";

        return Regex.Escape(character.ToString());
    }

    /// <summary>
    /// Tells whether a run of digits is a phone number rather than a date, a time, or a decimal number.
    /// </summary>
    /// <param name="candidate">The run of digits, as the pattern found it.</param>
    private static bool IsPhoneNumber(string candidate) => candidate.Count(char.IsDigit) >= MIN_PHONE_DIGITS && !NotAPhoneNumberPattern().IsMatch(candidate);

    /// <summary>
    /// Replaces an address or a path, but leaves the punctuation after it, which belongs to the sentence.
    /// </summary>
    /// <param name="report">What was replaced so far.</param>
    /// <param name="placeholder">The placeholder.</param>
    /// <param name="value">What the pattern found.</param>
    /// <returns>The placeholder and the punctuation after it.</returns>
    private static string ReplaceWithoutEnd(List<BflReplacement> report, string placeholder, string value)
    {
        var length = value.Length;
        while (length > 0)
        {
            var last = value[length - 1];
            var opened = value[..length].Count(character => character == OpeningOf(last));
            var closed = value[..length].Count(character => character == last);
            if (last is '.' or ',' or ';' or ':' or '!' or '?' or '*' or '_' or '\'' or '"' || last is ')' or ']' or '}' && closed > opened)
                length--;
            else
                break;
        }

        return length is 0 ? value : Record(report, placeholder, value[..length]) + value[length..];
    }

    /// <summary>
    /// Gets the opening bracket of a closing one, or a character no text holds.
    /// </summary>
    /// <param name="closing">The closing bracket.</param>
    private static char OpeningOf(char closing) => closing switch
    {
        ')' => '(',
        ']' => '[',
        '}' => '{',

        _ => '\0',
    };

    /// <summary>
    /// Notes a replacement in the report.
    /// </summary>
    /// <param name="report">What was replaced so far.</param>
    /// <param name="placeholder">The placeholder.</param>
    /// <param name="original">What it replaces.</param>
    /// <returns>The placeholder.</returns>
    private static string Record(List<BflReplacement> report, string placeholder, string original)
    {
        var index = report.FindIndex(replacement => replacement.Placeholder == placeholder && replacement.Original == original);
        if (index < 0)
            report.Add(new BflReplacement(placeholder, original, 1));
        else
            report[index] = report[index] with { Count = report[index].Count + 1 };

        return placeholder;
    }

    /// <summary>
    /// Gets a placeholder, e.g., "[Person 1]".
    /// </summary>
    /// <param name="label">What it stands for.</param>
    private static string Placeholder(string label) => $"[{label}]";

    /// <summary>
    /// A link in Markdown, whose text stays and whose address goes.
    /// </summary>
    [GeneratedRegex("""\[(?<label>[^\]\n]*)\]\(\s*<?(?<url>(?:[a-z][a-z0-9+.-]*:|www\.)[^\s<>()]*(?:\([^\s<>()]*\)[^\s<>()]*)*)>?(?:\s+"[^"\n]*")?\s*\)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkPattern();

    /// <summary>
    /// An address in angle brackets, e.g., "&lt;https://example.org&gt;".
    /// </summary>
    [GeneratedRegex(@"<(?<url>(?:https?|ftp|file|mailto):[^\s<>]+)>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AutoLinkPattern();

    /// <summary>
    /// An address of a web page or a file, with its scheme or beginning with "www.".
    /// </summary>
    [GeneratedRegex(@"(?<![\p{L}\p{N}@.])(?:(?:https?|ftp|file)://|www\.)[^\s<>""'`]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();

    /// <summary>
    /// An e-mail address.
    /// </summary>
    [GeneratedRegex(@"(?<![\p{L}\p{N}._%+-])[\p{L}\p{N}._%+-]+@[\p{L}\p{N}-]+(?:\.[\p{L}\p{N}-]+)*\.\p{L}{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    /// <summary>
    /// A path of a file or a folder: on Windows with its drive or its server, otherwise beginning with "/" or "~" and
    /// with at least two folders.
    /// </summary>
    [GeneratedRegex(@"(?<![\p{L}\p{N}])[A-Za-z]:[\\/][^\s""<>|*?]*|\\\\[^\s\\""<>|*?]+(?:\\[^\s""<>|*?]*)+|%[A-Za-z_]+%\\[^\s""<>|*?]*|(?<![\p{L}\p{M}\p{N}/.:~\\\])_-])(?:(?:~|\$HOME)(?:/[\p{L}\p{M}\p{N}._@+-]+)+|(?:/[\p{L}\p{M}\p{N}._@+-]+){2,})/?", RegexOptions.CultureInvariant)]
    private static partial Regex PathPattern();

    /// <summary>
    /// A run which may be a phone number: beginning with "+" or "0", with spaces, periods, slashes, hyphens, or
    /// parentheses between the digits; IsPhoneNumber decides.
    /// </summary>
    [GeneratedRegex(@"(?<![\p{L}\p{N}+(])\(?(?:\+|0)\d[\d \t./()-]{5,}\d(?![\p{L}\p{N}])", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    /// <summary>
    /// What looks like a phone number but is not: a date, a span of time, or a decimal number.
    /// </summary>
    [GeneratedRegex(@"^\(?\d{1,2}\.\d{1,2}\.(?:\d{4}|\d{2})$|^\d{1,2}[.:]\d{2}\s*-\s*\d{1,2}[.:]\d{2}$|^0[.,]", RegexOptions.CultureInvariant)]
    private static partial Regex NotAPhoneNumberPattern();

    /// <summary>
    /// A remark in parentheses after a name, e.g., "(TUM)".
    /// </summary>
    [GeneratedRegex(@"\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex ParenthesesPattern();

    /// <summary>
    /// A title on its own, e.g., "Prof." or "Dr.".
    /// </summary>
    [GeneratedRegex($@"^{TITLE}\.?$", RegexOptions.CultureInvariant)]
    private static partial Regex TitlePattern();
}