using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// Reads the arguments a model passes to a tool, and refuses wrong ones.
/// </summary>
/// <remarks>
/// A wrong argument is refused rather than guessed at: a placeholder such as 0 is not a page, and
/// quietly reading it as "no page" would do something the model did not ask for. The model reads
/// the refusal and tries again, so every refusal says what arrived, what would have been right,
/// and, for an optional argument, that leaving it out is always an option. A model which believes
/// the argument has to be there otherwise keeps trying placeholders, and every attempt costs one of
/// the tool calls an answer may make.<br/><br/>
/// A null counts the same as leaving an argument out: with a strict schema, a model has to pass
/// every argument and passes null for one it does not want to set.
/// </remarks>
internal static class ToolArgumentReader
{
    /// <summary>
    /// How much of a wrongly passed argument a refusal repeats back to the model.
    /// </summary>
    /// <remarks>
    /// Enough for a GUID in quotes. The model sent the value itself, so repeating all of it back
    /// only costs tokens.
    /// </remarks>
    private const int MAX_ARGUMENT_ECHO_LENGTH = 40;

    /// <summary>
    /// Reads a string argument the model always has to pass.
    /// </summary>
    /// <param name="arguments">The arguments the model passed.</param>
    /// <param name="propertyName">The argument.</param>
    /// <returns>The value, trimmed and never empty.</returns>
    /// <exception cref="ArgumentException">The argument is missing, no string, or empty.</exception>
    public static string ReadRequiredString(JsonElement arguments, string propertyName)
    {
        if (!TryGetArgument(arguments, propertyName, out var value))
            throw new ArgumentException($"Missing required argument '{propertyName}'.");

        var text = ReadString(propertyName, value, whenLeftOut: null);
        if (string.IsNullOrWhiteSpace(text))
            throw InvalidArgument(propertyName, value, "a non-empty string", whenLeftOut: null);

        return text;
    }

    /// <summary>
    /// Reads an optional string argument.
    /// </summary>
    /// <param name="arguments">The arguments the model passed.</param>
    /// <param name="propertyName">The argument.</param>
    /// <param name="whenLeftOut">What happens without the argument, completing "Leave it out ...".</param>
    /// <returns>The value, trimmed, or null when the model left the argument out.</returns>
    /// <exception cref="ArgumentException">The argument is no string.</exception>
    public static string? ReadOptionalString(JsonElement arguments, string propertyName, string whenLeftOut)
    {
        if (!TryGetArgument(arguments, propertyName, out var value))
            return null;

        return ReadString(propertyName, value, whenLeftOut);
    }

    /// <summary>
    /// Reads an optional argument which has to be a positive integer.
    /// </summary>
    /// <param name="arguments">The arguments the model passed.</param>
    /// <param name="propertyName">The argument.</param>
    /// <param name="whenLeftOut">What happens without the argument, completing "Leave it out ...".</param>
    /// <returns>The value, or null when the model left the argument out.</returns>
    /// <exception cref="ArgumentException">The argument is no positive integer.</exception>
    public static int? ReadOptionalPositiveInt(JsonElement arguments, string propertyName, string whenLeftOut)
    {
        if (!TryGetArgument(arguments, propertyName, out var value))
            return null;

        if (value.ValueKind is not JsonValueKind.Number || !value.TryGetInt32(out var intValue) || intValue <= 0)
            throw InvalidArgument(propertyName, value, "a positive integer", whenLeftOut);

        return intValue;
    }

    /// <summary>
    /// Reads an optional argument which has to be one of the values the tool offers.
    /// </summary>
    /// <remarks>
    /// The values are compared exactly, because the schema offers them exactly so.
    /// </remarks>
    /// <param name="arguments">The arguments the model passed.</param>
    /// <param name="propertyName">The argument.</param>
    /// <param name="allowedValues">The values the tool offers.</param>
    /// <param name="whenLeftOut">What happens without the argument, completing "Leave it out ...".</param>
    /// <returns>The value, or null when the model left the argument out.</returns>
    /// <exception cref="ArgumentException">The argument is none of the offered values.</exception>
    public static string? ReadOptionalChoice(JsonElement arguments, string propertyName, IReadOnlyCollection<string> allowedValues, string whenLeftOut)
    {
        if (!TryGetArgument(arguments, propertyName, out var value))
            return null;

        if (!TryReadChoice(value, allowedValues, out var choice))
            throw InvalidArgument(propertyName, value, $"one of {string.Join(", ", allowedValues)}", whenLeftOut);

        return choice;
    }

    /// <summary>
    /// Reads an optional argument which has to be a list of values the tool offers.
    /// </summary>
    /// <remarks>
    /// The values are compared exactly, because the schema offers them exactly so. An empty list is
    /// refused rather than read as leaving the argument out: it asks for none of the values, and
    /// what leaving it out does instead is for the refusal to say. A value the model names twice
    /// counts once.
    /// </remarks>
    /// <param name="arguments">The arguments the model passed.</param>
    /// <param name="propertyName">The argument.</param>
    /// <param name="allowedValues">The values the tool offers.</param>
    /// <param name="whenLeftOut">What happens without the argument, completing "Leave it out ...".</param>
    /// <returns>The values in the order the model named them, or null when it left the argument out.</returns>
    /// <exception cref="ArgumentException">The argument is no list, an empty one, or holds a value the tool does not offer.</exception>
    public static IReadOnlyList<string>? ReadOptionalChoices(JsonElement arguments, string propertyName, IReadOnlyCollection<string> allowedValues, string whenLeftOut)
    {
        if (!TryGetArgument(arguments, propertyName, out var value))
            return null;

        var offeredValues = string.Join(", ", allowedValues);
        if (value.ValueKind is not JsonValueKind.Array || value.GetArrayLength() == 0)
            throw InvalidArgument(propertyName, value, $"a list of one or more of {offeredValues}", whenLeftOut);

        var choices = new List<string>(value.GetArrayLength());
        foreach (var item in value.EnumerateArray())
        {
            if (!TryReadChoice(item, allowedValues, out var choice))
                throw InvalidListValue(propertyName, item, $"one of {offeredValues}", whenLeftOut);

            if (!choices.Contains(choice, StringComparer.Ordinal))
                choices.Add(choice);
        }

        return choices;
    }

    /// <summary>
    /// Looks up an argument, treating null the same as leaving it out.
    /// </summary>
    private static bool TryGetArgument(JsonElement arguments, string propertyName, out JsonElement value) =>
        arguments.TryGetProperty(propertyName, out value) && value.ValueKind is not JsonValueKind.Null;

    private static string ReadString(string propertyName, JsonElement value, string? whenLeftOut)
    {
        if (value.ValueKind is not JsonValueKind.String)
            throw InvalidArgument(propertyName, value, "a string", whenLeftOut);

        return value.GetString()?.Trim() ?? string.Empty;
    }

    private static bool TryReadChoice(JsonElement value, IReadOnlyCollection<string> allowedValues, [NotNullWhen(true)] out string? choice)
    {
        choice = value.ValueKind is JsonValueKind.String ? value.GetString()?.Trim() : null;
        return choice is not null && allowedValues.Contains(choice, StringComparer.Ordinal);
    }

    /// <summary>
    /// Builds the refusal of an argument the model passed wrongly.
    /// </summary>
    /// <param name="propertyName">The argument.</param>
    /// <param name="value">What the model passed, as it arrived.</param>
    /// <param name="expectation">What the argument must be, completing "must be ...".</param>
    /// <param name="whenLeftOut">What happens without the argument, completing "Leave it out ...", or null for a required one.</param>
    private static ArgumentException InvalidArgument(string propertyName, JsonElement value, string expectation, string? whenLeftOut) =>
        Refusal($"Argument '{propertyName}' must be {expectation}, but was {Echo(value)}.", whenLeftOut);

    /// <summary>
    /// Builds the refusal of a list the model passed with a wrong value in it.
    /// </summary>
    /// <remarks>
    /// Only the wrong value is repeated back, not the whole list: the model has to find out which
    /// of its values the tool means.
    /// </remarks>
    private static ArgumentException InvalidListValue(string propertyName, JsonElement item, string expectation, string whenLeftOut) =>
        Refusal($"Every value of argument '{propertyName}' must be {expectation}, but one was {Echo(item)}.", whenLeftOut);

    private static ArgumentException Refusal(string message, string? whenLeftOut) => new(whenLeftOut is null ? message : $"{message} Leave it out {whenLeftOut}.");

    private static string Echo(JsonElement value)
    {
        var receivedValue = value.GetRawText();
        return receivedValue.Length > MAX_ARGUMENT_ECHO_LENGTH ? $"{receivedValue[..MAX_ARGUMENT_ECHO_LENGTH]}..." : receivedValue;
    }
}