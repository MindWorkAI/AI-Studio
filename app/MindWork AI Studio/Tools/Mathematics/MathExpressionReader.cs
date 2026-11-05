using System.Globalization;
using System.Text.RegularExpressions;

using AIStudio.Chat;

using AngouriMath;
using AngouriMath.Core.Exceptions;

using PeterO.Numbers;

namespace AIStudio.Tools.Mathematics;

/// <summary>
/// Reads an expression of the model into AngouriMath, strictly.
/// </summary>
/// <remarks>
/// AngouriMath reads some spellings as something else than they say, silently, see
/// AngouriMathBehaviorTests: without explicit parsing, x2 is x^2 and foo(3) is foo times 3, and
/// even with it, a != b is the factorial of a equated to b. This reader refuses such spellings
/// instead of letting a model compute what it did not mean, and states how AngouriMath writes the
/// thing instead. Its parse errors list over a thousand characters of expected tokens; the reader
/// keeps the column and the cause.<br/><br/>
/// Call it inside a computation of MathComputationService: the parser needs the stack of its thread.
/// </remarks>
public static partial class MathExpressionReader
{
    private const int MAX_REASON_LENGTH = 200;

    /// <summary>
    /// Reads an expression, or refuses it.
    /// </summary>
    /// <param name="text">The expression as the model wrote it.</param>
    /// <param name="argumentName">The argument the expression came in, named in every message.</param>
    /// <returns>The expression as AngouriMath reads it.</returns>
    /// <exception cref="MathComputationException">The expression is empty, too long, uses a spelling AngouriMath reads otherwise, or cannot be read.</exception>
    public static Entity Read(string text, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new MathComputationException($"Argument '{argumentName}' is empty.");

        if (text.Length > MathLimits.MAX_EXPRESSION_LENGTH)
            throw new MathComputationException(string.Create(CultureInfo.InvariantCulture, $"Argument '{argumentName}' has {text.Length:N0} characters, but at most {MathLimits.MAX_EXPRESSION_LENGTH:N0} are read."));

        if (text.Contains("!=", StringComparison.Ordinal))
            throw new MathComputationException($"Argument '{argumentName}' contains '!=', which AngouriMath reads as a factorial followed by '='. Not-equal is written '<>'.");

        if (text.Contains('|'))
            throw new MathComputationException($"Argument '{argumentName}' contains '|', which AngouriMath reads as divisibility. The absolute value is written abs(x), and 'or' joins two statements.");

        var equalsSigns = CountEqualsSigns(text);
        if (equalsSigns > 1)
            throw new MathComputationException($"Argument '{argumentName}' contains {equalsSigns} equals signs, but an expression holds one equation at most. AngouriMath would read a chain of them as several equations joined by 'and'.");

        var expression = Parse(text, argumentName);
        if (expression.Vars.Any(variable => variable.Name == "oo"))
            throw new MathComputationException($"Argument '{argumentName}' uses 'oo', which AngouriMath reads as a variable. Infinity is written +oo or -oo.");

        return expression;
    }

    private static Entity Parse(string text, string argumentName)
    {
        try
        {
            using var explicitParsing = MathS.Settings.ExplicitParsingOnly.Set(true);
            Entity parsed;

            // Without the cache, an expression parsed elsewhere with other settings cannot come back here:
            using (MathS.Settings.DowncastingEnabled.Set(false))
                parsed = MathS.FromString(text, useCache: false);

            return WithExactLiterals(parsed);
        }
        catch (MissingOperatorParseException exception)
        {
            throw new MathComputationException(DescribeMissingOperator(exception.Message, argumentName));
        }
        catch (FunctionArgumentCountException exception)
        {
            throw new MathComputationException($"Argument '{argumentName}' cannot be read: {exception.Message}.");
        }
        catch (ParseException exception)
        {
            throw new MathComputationException(DescribeParseError(exception.Message, argumentName));
        }
    }

    /// <summary>
    /// Turns every decimal literal into the fraction it writes, e.g., 1.5e-3 into 3/2000.
    /// </summary>
    /// <remarks>
    /// AngouriMath downcasts while it parses: it reads 0.1 as 1/10, but 6.62607015e-34 as an inexact
    /// decimal and 1e-60 as 0, see AngouriMathBehaviorTests. Simplifying then drops an inexact decimal
    /// from a sum, so 1 + 1e-40 would be exactly 1. The reader therefore parses without downcasting,
    /// which leaves every number a decimal, and turns each into its fraction or integer. A literal
    /// with an exponent beyond MathLimits.MAX_EXACT_DIGITS stays a decimal.
    /// </remarks>
    private static Entity WithExactLiterals(Entity expression) => expression.Replace(node => node is Entity.Number.Real literal and not Entity.Number.Rational ? ExactValueOf(literal) : node);

    private static Entity ExactValueOf(Entity.Number.Real literal)
    {
        var value = literal.EDecimal;
        if (value.IsNaN() || value.IsInfinity() || value.Exponent.Abs().CompareTo(EInteger.FromInt32(MathLimits.MAX_EXACT_DIGITS)) > 0)
            return literal;

        var fraction = ERational.FromEDecimal(value);
        return fraction.Denominator.Equals(EInteger.One) ? Entity.Number.Integer.Create(fraction.Numerator) : Entity.Number.Rational.Create(fraction.Numerator, fraction.Denominator);
    }

    private static string DescribeMissingOperator(string message, string argumentName)
    {
        var match = MissingOperatorTokens().Match(message);
        if (!match.Success)
            return $"Argument '{argumentName}' lacks an operator between two operands. Multiplication is written '*', as in 2*x.";

        var left = match.Groups["left"].Value;
        var right = match.Groups["right"].Value;
        var column = int.Parse(match.Groups["column"].Value, CultureInfo.InvariantCulture) + 1;
        if (right == "(" && IsName(left))
            return $"Argument '{argumentName}' has no operator between '{left}' and '(' at column {column}. AngouriMath knows no function named '{left}', so it would read the name as a variable times the parentheses. Multiplication is written '*'.";

        return $"Argument '{argumentName}' has no operator between '{left}' and '{right}' at column {column}. Multiplication is written '*', as in 2*x, and a function takes its argument in parentheses, as in sin(x).";
    }

    private static string DescribeParseError(string message, string argumentName)
    {
        var match = ParseErrorPosition().Match(message);
        if (!match.Success)
            return $"Argument '{argumentName}' cannot be read: {message.Shorten(MAX_REASON_LENGTH)}";

        var column = int.Parse(match.Groups["column"].Value, CultureInfo.InvariantCulture) + 1;
        var reason = match.Groups["reason"].Value.Shorten(MAX_REASON_LENGTH);
        return $"Argument '{argumentName}' cannot be read at column {column}: {reason}.";
    }

    private static bool IsName(string token) => token.Length > 0 && token.All(character => char.IsLetterOrDigit(character) || character == '_');

    /// <summary>
    /// Counts the equals signs which stand for an equation, leaving out those of &lt;= and &gt;=.
    /// </summary>
    private static int CountEqualsSigns(string text)
    {
        var count = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '=')
                continue;

            if (index > 0 && text[index - 1] is '<' or '>')
                continue;

            count++;
        }

        return count;
    }

    // There should be an operator between [@0,0:0='2',<185>,1:0] and [@1,1:1='x',<189>,1:1], while parsing `2x`
    [GeneratedRegex(@"='(?<left>.*?)',<\d+>,\d+:\d+\] and \[@\d+,\d+:\d+='(?<right>.*?)',<\d+>,\d+:(?<column>\d+)\]")]
    private static partial Regex MissingOperatorTokens();

    // line 1:4 extraneous input '*' expecting {'-', '+', ...}, while parsing `2 + * 3`
    [GeneratedRegex(@"^line \d+:(?<column>\d+) (?<reason>.*?)(?: expecting .*)?(?:, while parsing `.*`)?$", RegexOptions.Singleline)]
    private static partial Regex ParseErrorPosition();
}