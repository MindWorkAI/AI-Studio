using System.Collections.Frozen;
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
/// keeps the column and the cause. Where AngouriMath refuses a name of its own, its message names
/// the library, its C# API, and functions the tools do not support; the reader keeps the name.<br/><br/>
/// It also refuses the functions which AngouriMath reads since version 2.7.0 or 2.8.0, except those the
/// tools return, see UNSUPPORTED_FUNCTIONS and UNSUPPORTED_NODE_TYPES.<br/><br/>
/// Call it inside a computation of MathComputationService: the parser needs the stack of its thread.
/// </remarks>
public static partial class MathExpressionReader
{
    private const int MAX_REASON_LENGTH = 200;

    private const int MAX_SHOWN_LENGTH = 120;

    /// <summary>
    /// The functions which AngouriMath reads since version 2.7.0 or 2.8.0, and which the tools do not support.
    /// </summary>
    /// <remarks>
    /// AngouriMath computes some of them while it parses, such as det, dot, or jacobian of a matrix written
    /// out, so nothing of them is left in the expression to refuse afterward. The reader therefore refuses
    /// them by name before parsing, also those which AngouriMath refuses itself, such as nextprime, since its
    /// messages name the library. lambertw, productlog, polygamma, and digamma stay readable, since solving,
    /// integrating, and differentiating return them. AngouriMathBehaviorTests lists every name AngouriMath reads.
    /// </remarks>
    internal static readonly FrozenSet<string> UNSUPPORTED_FUNCTIONS = new[]
    {
        "arg", "beta", "catalannumber", "conj", "conjugate", "cross", "curl", "det", "divergence", "divisors", "divisorsigma", "dot",
        "doublefactorial", "factorial2", "factorialpower", "factorization", "factorize", "fallingfactorial", "forwarddifference",
        "fractionalpart", "gradient", "harmonicnumber", "heaviside", "hessian", "hurwitzzeta", "im", "Im", "imag", "isprime", "jacobian",
        "laplacian", "mobius", "moebiusmu", "multinomial", "nextprime", "norm", "partialderivative", "pochhammer", "primefactors", "primenu",
        "primeomega", "primepi", "quotient", "re", "Re", "real", "resultant", "riemannzeta", "risingfactorial", "trace", "transpose",
        "unitstep", "zeta"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The kinds of nodes which AngouriMath has since version 2.7.0 or 2.8.0, and which the tools do not support.
    /// </summary>
    /// <remarks>
    /// Most of them come from a function of UNSUPPORTED_FUNCTIONS, which the reader refuses by name before.
    /// Three come without a name: the double factorial n!!, the entry A[1, 2] of a matrix, and the product
    /// over a set, product(f, x in S). LambertWf, IncompleteGammaf, which gamma(a, z) reads into, and
    /// Polygammaf stay readable, since solving, integrating, and differentiating return them.
    /// </remarks>
    internal static readonly FrozenSet<Type> UNSUPPORTED_NODE_TYPES = new[]
    {
        typeof(Entity.Argf), typeof(Entity.Betaf), typeof(Entity.CatalanNumberf), typeof(Entity.Conjugatef), typeof(Entity.CrossProductf),
        typeof(Entity.Determinantf), typeof(Entity.DivisorSigmaf), typeof(Entity.DotProductf), typeof(Entity.DoubleFactorialf),
        typeof(Entity.FallingFactorialf), typeof(Entity.HarmonicNumberf), typeof(Entity.Imf), typeof(Entity.MatrixEntryf), typeof(Entity.Mobiusf),
        typeof(Entity.Normf), typeof(Entity.PartialDerivativef), typeof(Entity.PrimeFactorizationf), typeof(Entity.Set.PrimeFactorsf),
        typeof(Entity.PrimeNuf), typeof(Entity.PrimeOmegaf), typeof(Entity.PrimePif), typeof(Entity.ProductOverSetf), typeof(Entity.Ref),
        typeof(Entity.RisingFactorialf), typeof(Entity.Tracef), typeof(Entity.Transposef), typeof(Entity.Zetaf)
    }.ToFrozenSet();

    /// <summary>
    /// Reads an expression, or refuses it.
    /// </summary>
    /// <param name="text">The expression as the model wrote it.</param>
    /// <param name="argumentName">The argument the expression came in, named in every message.</param>
    /// <returns>The expression as AngouriMath reads it.</returns>
    /// <exception cref="MathComputationException">The expression is empty, too long, uses a spelling AngouriMath reads otherwise or a function the tools do not support, or cannot be read.</exception>
    public static Entity Read(string text, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new MathComputationException($"Argument '{argumentName}' is empty.");

        if (text.Length > MathLimits.MAX_EXPRESSION_LENGTH)
            throw new MathComputationException(string.Create(CultureInfo.InvariantCulture, $"Argument '{argumentName}' has {text.Length:N0} characters, but at most {MathLimits.MAX_EXPRESSION_LENGTH:N0} are read."));

        if (text.Contains("!=", StringComparison.Ordinal))
            throw new MathComputationException($"Argument '{argumentName}' contains '!=', which the tool reads as a factorial followed by '='. Not-equal is written '<>'.");

        if (text.Contains('|'))
            throw new MathComputationException($"Argument '{argumentName}' contains '|', which the tool reads as divisibility. The absolute value is written abs(x), and 'or' joins two statements.");

        var equalsSigns = CountEqualsSigns(text);
        if (equalsSigns > 1)
            throw new MathComputationException($"Argument '{argumentName}' contains {equalsSigns} equals signs, but an expression holds one equation at most. The tool would read a chain of them as several equations joined by 'and'.");

        var unsupportedFunction = FunctionNames().Matches(text).Select(match => match.Groups["name"].Value).FirstOrDefault(UNSUPPORTED_FUNCTIONS.Contains);
        if (unsupportedFunction is not null)
            throw new MathComputationException($"Argument '{argumentName}' uses the function '{unsupportedFunction}', which the tool does not support.");

        var expression = Parse(text, argumentName);
        if (expression.Vars.Any(variable => variable.Name == "oo"))
            throw new MathComputationException($"Argument '{argumentName}' uses 'oo', which the tool reads as a variable. Infinity is written +oo or -oo.");

        var unsupportedNode = expression.Nodes.FirstOrDefault(node => UNSUPPORTED_NODE_TYPES.Contains(node.GetType()));
        if (unsupportedNode is not null)
            throw new MathComputationException($"Argument '{argumentName}' contains '{unsupportedNode.ToString().Shorten(MAX_SHOWN_LENGTH)}', which the tool does not support.");

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
        catch (UnrecognizedFunctionParseException exception)
        {
            throw new MathComputationException(DescribeUnrecognizedFunction(exception.Message, argumentName));
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
    /// decimal and 1e-60 as 0, see AngouriMathBehaviorTests. An inexact decimal leaves every value it
    /// takes part in inexact, and up to version 2.6.0, simplifying even dropped it from a sum, so
    /// 1 + 1e-40 was exactly 1. The reader therefore parses without downcasting, which leaves every
    /// number a decimal, and turns each into its fraction or integer. A literal with an exponent beyond
    /// MathLimits.MAX_EXACT_DIGITS stays a decimal.
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
            return $"Argument '{argumentName}' has no operator between '{left}' and '(' at column {column}. The tool knows no function named '{left}', so it would read the name as a variable times the parentheses. Multiplication is written '*'.";

        return $"Argument '{argumentName}' has no operator between '{left}' and '{right}' at column {column}. Multiplication is written '*', as in 2*x, and a function takes its argument in parentheses, as in sin(x).";
    }

    private static string DescribeUnrecognizedFunction(string message, string argumentName)
    {
        var match = UnrecognizedFunctionName().Match(message);
        return match.Success
            ? $"Argument '{argumentName}' uses the function '{match.Groups["name"].Value}', which the tool does not support."
            : $"Argument '{argumentName}' uses a function or an operator which the tool does not support.";
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

    // zeta(3), but neither x_zeta(3) nor zeta (3): the parser reads no function in either
    [GeneratedRegex(@"(?<![\p{L}\p{N}_])(?<name>\p{L}[\p{L}\p{N}_]*)\(")]
    private static partial Regex FunctionNames();

    // There should be an operator between [@0,0:0='2',<185>,1:0] and [@1,1:1='x',<189>,1:1], while parsing `2x`
    [GeneratedRegex(@"='(?<left>.*?)',<\d+>,\d+:\d+\] and \[@\d+,\d+:\d+='(?<right>.*?)',<\d+>,\d+:(?<column>\d+)\]")]
    private static partial Regex MissingOperatorTokens();

    // there is no function trunc: AngouriMath has no rounding functions. …, or: isprime is not a function in AngouriMath: …
    [GeneratedRegex(@"^(?:there is no function (?<name>[^\s:]+):|(?<name>[^\s:]+) is not a function\b)")]
    private static partial Regex UnrecognizedFunctionName();

    // line 1:4 extraneous input '*' expecting {'-', '+', ...}, while parsing `2 + * 3`
    [GeneratedRegex(@"^line \d+:(?<column>\d+) (?<reason>.*?)(?: expecting .*)?(?:, while parsing `.*`)?$", RegexOptions.Singleline)]
    private static partial Regex ParseErrorPosition();
}