using AngouriMath;
using AngouriMath.Core;
using AngouriMath.Core.Exceptions;

using PeterO.Numbers;

namespace AIStudio.Tests.Tools.Mathematics;

/// <summary>
/// Pins the behavior of AngouriMath which the Mathematics tools build on.
/// </summary>
/// <remarks>
/// AngouriMath puts correct answers ahead of backward compatibility, and says so: the release notes of
/// 2.6.0 list 516 changed answers, fourteen of them silent, where the call still succeeds and returns
/// something else. An update which changes anything below fails here first, not in a chat. Each test
/// names what AngouriMath does; where the tools have to guard against it, the test says so.
/// </remarks>
[TestFixture]
public sealed class AngouriMathBehaviorTests
{
    private static readonly Entity.Variable X = MathS.Var("x");

    private static Entity Parse(string text)
    {
        using var _ = MathS.Settings.ExplicitParsingOnly.Set(true);
        return MathS.FromString(text);
    }

    private static string DecimalOf(string text, int workingPrecision, int digits)
    {
        using var _ = MathS.Settings.DecimalPrecisionContext.Set(EContext.ForPrecision(workingPrecision));
        return Parse(text).EvalNumerical().RealPart.EDecimal.RoundToPrecision(EContext.ForPrecision(digits)).ToString();
    }

    [Test]
    public void RationalsStayExact()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Parse("547 / 57576").Evaled, Is.EqualTo(Entity.Number.Rational.Create(547, 57576)));
            Assert.That(Parse("0.1 + 0.2").Evaled, Is.EqualTo(Entity.Number.Rational.Create(3, 10)), "A decimal literal is read as the rational it writes.");
            Assert.That(Parse("1.5e-3").Evaled, Is.EqualTo(Entity.Number.Rational.Create(3, 2000)));
            Assert.That(Parse("2^200 / 3^100").Evaled, Is.TypeOf<Entity.Number.Rational>());
            Assert.That(Parse("(-8)^(1/3)").Evaled, Is.EqualTo(Entity.Number.Integer.Create(-2)), "The cube root of a negative integer is the real one.");
        }
    }

    [Test]
    public void IrrationalValuesAreDecimalsOfTheCurrentPrecision()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MathS.Settings.DecimalPrecisionContext.Value.Precision.ToInt32Checked(), Is.EqualTo(100), "The default precision is 100 significant digits.");
            Assert.That(Parse("sqrt(2)").Evaled, Is.TypeOf<Entity.Number.Real>());
            Assert.That(Parse("sqrt(2)").InnerSimplified.ToString(), Is.EqualTo("sqrt(2)"), "Simplifying keeps an irrational value exact.");
        }
    }

    [Test]
    public void ElementaryFunctionsAreRightToTheLastDigit()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DecimalOf("sin(1)", 60, 60), Is.EqualTo("0.841470984807896506652502321630298999622563060798371065672752"));
            Assert.That(DecimalOf("e", 60, 60), Is.EqualTo("2.71828182845904523536028747135266249775724709369995957496697"));
            Assert.That(DecimalOf("pi", 60, 60), Is.EqualTo("3.14159265358979323846264338327950288419716939937510582097494"));
            Assert.That(DecimalOf("ln(2)", 60, 60), Is.EqualTo("0.693147180559945309417232121458176568075500134360255254120680"));
            Assert.That(DecimalOf("sqrt(2)", 60, 60), Is.EqualTo("1.41421356237309504880168872420969807856967187537694807317668"));
        }
    }

    [Test]
    public void ACompoundExpressionNeedsGuardDigits()
    {
        // pi^4/15 is 6.4939394022668291490960221792470074166485057115123614460978572926..., see Python's decimal:
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DecimalOf("pi^4/15", 60, 60), Is.Not.EqualTo("6.49393940226682914909602217924700741664850571151236144609786"), "At the precision asked for, the last digits are off: every step rounds.");
            Assert.That(DecimalOf("pi^4/15", 70, 60), Is.EqualTo("6.49393940226682914909602217924700741664850571151236144609786"), "Ten guard digits make up for it.");
        }
    }

    [Test]
    public void SettingsBelongToTheFlowWhichSetsThem()
    {
        var inner = Task.Run(() =>
        {
            using var _ = MathS.Settings.DecimalPrecisionContext.Set(EContext.ForPrecision(30));
            return MathS.Settings.DecimalPrecisionContext.Value.Precision.ToInt32Checked();
        }).Result;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(inner, Is.EqualTo(30));
            Assert.That(MathS.Settings.DecimalPrecisionContext.Value.Precision.ToInt32Checked(), Is.EqualTo(100), "A setting made in another flow does not leak into this one.");
        }
    }

    [Test]
    public void ExplicitParsingRefusesImplicitMultiplication()
    {
        // Without explicit parsing, x2 is x^2, sin x is the variable sin times x, and foo(3) is foo times 3:
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MathS.FromString("x2").ToString(), Is.EqualTo("x ^ 2"));
            Assert.That(MathS.FromString("sin x").ToString(), Is.EqualTo("sin * x"));
            Assert.That(MathS.FromString("foo(3)").ToString(), Is.EqualTo("foo * 3"));

            foreach (var text in new[] { "2x", "x y", "x2", "sin x", "foo(3)", "2 pi", "x(x+1)", "(x+1)(x-1)" })
                Assert.Throws<MissingOperatorParseException>(() => Parse(text), text);
        }
    }

    [Test]
    public void ExplicitParsingReadsTheSyntaxTheToolsDocument()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Parse("2*(x+1)").ToString(), Is.EqualTo("2 * (x + 1)"));
            Assert.That(Parse("-x^2").ToString(), Is.EqualTo("-x ^ 2"));
            Assert.That(Parse("3!").Evaled, Is.EqualTo(Entity.Number.Integer.Create(6)));
            Assert.That(Parse("log(100)").Evaled, Is.EqualTo(Entity.Number.Integer.Create(2)), "log without a base is the decimal logarithm.");
            Assert.That(Parse("log(2, 8)").Evaled, Is.EqualTo(Entity.Number.Integer.Create(3)), "The base comes first.");
            Assert.That(Parse("ln(e)").Evaled, Is.EqualTo(Entity.Number.Integer.Create(1)));
            Assert.That(Parse("exp(x)").ToString(), Is.EqualTo("e ^ x"));
            Assert.That(Parse("e^(i*pi)").Evaled, Is.EqualTo(Entity.Number.Integer.Create(-1)));
            Assert.That(Parse("π"), Is.TypeOf<Entity.Constant>());
            Assert.That(Parse("gamma(5)").Evaled, Is.EqualTo(Entity.Number.Integer.Create(24)));
            Assert.That(Parse("binomial(5, 2)").Evaled, Is.EqualTo(Entity.Number.Integer.Create(10)));
            Assert.That(Parse("abs(-2)").Evaled, Is.EqualTo(Entity.Number.Integer.Create(2)));
            Assert.That(Parse("a <> b").ToString(), Is.EqualTo("not a = b"));
            Assert.That(Parse("x_1 + x_2").ToString(), Is.EqualTo("x_1 + x_2"));
            Assert.That(Parse("integral(x^3/(e^x-1), x, 0, +oo)"), Is.TypeOf<Entity.Integralf>());
            Assert.That(Parse("limit(sin(x)/x, x, 0)"), Is.TypeOf<Entity.Limitf>());
            Assert.That(Parse("+oo"), Is.TypeOf<Entity.Number.Real>());
        }
    }

    [Test]
    public void SomeSpellingsReadAsSomethingElse()
    {
        // The tools refuse these before parsing:
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Parse("a != b").ToString(), Is.EqualTo("a! = b"), "!= is the factorial of a, equated to b.");
            Assert.That(Parse("oo"), Is.TypeOf<Entity.Variable>(), "Only +oo and -oo are infinite; oo is a variable.");
            Assert.That(Parse("x | y"), Is.TypeOf<Entity.Dividesf>(), "| is divisibility, since 2.6.0.");
            Assert.That(Parse("2 = 2 = 2").ToString(), Is.EqualTo("2 = 2 and 2 = 2"), "A chain of equations is a conjunction.");
        }
    }

    [Test]
    public void UnreadableInputThrowsAParseException()
    {
        using (Assert.EnterMultipleScope())
        {
            // Under explicit parsing, a character the lexer drops can leave two operands side by side, as in 2·3:
            foreach (var text in new[] { "2 + * 3", "(2 + 3", "", "√2", "x²", "2·3", "1,5", "2**3", "|x|", "\\frac{1}{2}" })
                Assert.That(() => Parse(text), Throws.InstanceOf<ParseException>(), text);

            Assert.Throws<FunctionArgumentCountException>(() => Parse("sin(1, 2)"));
            Assert.Throws<FunctionArgumentCountException>(() => Parse("integral(x^2, x, 2)"), "An integral has two or four arguments.");
        }
    }

    [Test]
    public void AParseExceptionNamesLineAndColumn()
    {
        var exception = Assert.Throws<UnhandledParseException>(() => Parse("2 + * 3"))!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Message, Does.StartWith("line 1:4 extraneous input '*' expecting {"), "The message lists every token the parser expected, over a thousand characters.");
            Assert.That(exception.Message, Does.EndWith(", while parsing `2 + * 3`"));
        }
    }

    [Test]
    public void FreeVariablesLeaveOutBoundOnes()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Parse("integral(x^2, x, 0, 1)").FreeVariables, Is.Empty);
            Assert.That(Parse("integral(x*a, x, 0, 1)").FreeVariables, Is.EquivalentTo(new[] { MathS.Var("a") }));
            Assert.That(Parse("pi + x").FreeVariables, Has.Some.InstanceOf<Entity.Constant>(), "The constants count among the free variables, see MathVariables.");
            Assert.That(Parse("ln(0)").FreeVariables.Select(variable => variable.Name), Is.EqualTo(new[] { "e" }), "ln is the logarithm to the base e.");
            Assert.That(Parse("x + 1").EvaluableNumerical, Is.False);
            Assert.Throws<CannotEvalException>(() => Parse("x + 1").EvalNumerical());
        }
    }

    [Test]
    public void ValuesWithoutAMeaningAreNaN()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Parse("1/0").Evaled.ToString(), Is.EqualTo("NaN"));
            Assert.That(Parse("0/0").Evaled.ToString(), Is.EqualTo("NaN"));
            Assert.That(Parse("(-1)!").Evaled.ToString(), Is.EqualTo("NaN"));
            Assert.That(Parse("tan(pi/2)").Evaled.ToString(), Is.EqualTo("NaN"));
            Assert.That(Parse("ln(0)").Evaled.ToString(), Is.EqualTo("-oo"));
            Assert.That(Parse("sqrt(-1)").Evaled.ToString(), Is.EqualTo("i"));
        }
    }

    [Test]
    public void LatexComesFromLatexize()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Parse("547 / 57576").Evaled.Latexize(), Is.EqualTo(@"\frac{547}{57576}"));
            Assert.That(Parse("x^3/(e^x-1)").Latexize(), Is.EqualTo(@"\frac{{x}^{3}}{{\mathrm{e}}^{x}-1}"));
            Assert.That(Parse("integral(x^3/(e^x-1), x, 0, +oo)").Latexize(), Is.EqualTo(@"\int_{0}^{\infty } \frac{{x}^{3}}{{\mathrm{e}}^{x}-1}\,\mathrm{d}x"));
            Assert.That(Parse("(x-1)/(x-1)").Simplify().Latexize(), Is.EqualTo(@"1 \quad \text{for} \quad x-1 \neq 0"));
        }
    }

    [Test]
    public void CompiledFunctionsReturnNaNWhereTheyHaveNoValue()
    {
        var bose = Parse("x^3/(e^x-1)").Compile<double, double>(X);
        var root = Parse("sqrt(x)").Compile<double, double>(X);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(bose(2.0), Is.EqualTo(8.0 / (Math.Exp(2.0) - 1.0)).Within(1e-15));
            Assert.That(bose(0.0), Is.NaN, "0/0 at the lower bound of the integral; a quadrature must not evaluate there.");
            Assert.That(root(-1.0), Is.NaN, "A compiled function stays real.");
            Assert.That(Parse("erf(x)").Compile<double, double>(X)(1.0), Is.EqualTo(0.8427007929497149).Within(1e-15));
        }
    }

    [Test]
    public void SimplifyStatesWhereItsAnswerHolds()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Parse("sin(x)^2 + cos(x)^2").Simplify(), Is.EqualTo(Entity.Number.Integer.Create(1)));
            Assert.That(Parse("(x^2-1)/(x-1)").Simplify(), Is.TypeOf<Entity.Providedf>());
            Assert.That(Parse("(x^2-1)/(x-1)").Simplify().ToString(), Is.EqualTo("x + 1 provided not x - 1 = 0"));
            Assert.That(Parse("sqrt(x^2)").Simplify().ToString(), Is.EqualTo("sqrt(x ^ 2)"), "Over the complex numbers, sqrt(x^2) is not x.");
        }
    }

    [Test]
    public void ExpandAndFactorizeAreInverse()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Parse("(x+1)^3").Expand().ToString(), Is.EqualTo("1 + 3 * x + 3 * x ^ 2 + x ^ 3"));
            Assert.That(Parse("x^4 - 1").Factorize().ToString(), Is.EqualTo("(x + 1) * (x - 1) * (x ^ 2 + 1)"));
            Assert.That(Parse("x^2 - 5*x + 6").Factorize().ToString(), Is.EqualTo("(x - 3) * (x - 2)"));
        }
    }

    [Test]
    public void ExpandLeavesAnExpressionWithTooManyTermsAsItIs()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MathS.Settings.MaxExpansionTermCount.Value, Is.EqualTo(2000));
            Assert.That(Parse("(x+y+z+w)^60").Expand().ToString(), Is.EqualTo("(x + y + z + w) ^ 60"), "39,711 terms are above the limit.");
            Assert.That(Parse("(x+y)^100").Expand(), Is.TypeOf<Entity.Sumf>());
        }
    }

    [Test]
    public void DifferentiateNeedsASimplifyAfterwards()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Parse("x^5").Differentiate(X, 3).ToString(), Is.EqualTo("3 * x ^ 2 * 4 * 5"));
            Assert.That(Parse("x^5").Differentiate(X, 3).Simplify().ToString(), Is.EqualTo("60 * x ^ 2"));
            Assert.That(Parse("x^2*sin(x)").Differentiate(X).Simplify().ToString(), Is.EqualTo("x ^ 2 * cos(x) + 2 * x * sin(x)"));
        }
    }

    [Test]
    public void AnIndefiniteIntegralEndsInAConstantOrStaysAnIntegral()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Parse("x^2").Integrate(X).ToString(), Is.EqualTo("x ^ 3 / 3 + C"));
            Assert.That(Parse("C*x").Integrate(X).ToString(), Is.EqualTo("C * x ^ 2 / 2 + C_1"), "The constant takes a name the expression does not use.");
            Assert.That(Parse("e^(-x^2)").Integrate(X).ToString(), Is.EqualTo("sqrt(pi) / 2 * erf(x) + C"));
            Assert.That(Parse("sin(sin(x))").Integrate(X), Is.TypeOf<Entity.Integralf>(), "Without a closed form, the integral comes back.");
        }
    }

    [Test]
    public void ADefiniteIntegralIsExactThroughItsNode()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Parse("integral(x^2, x, 0, 1)").InnerSimplified, Is.EqualTo(Entity.Number.Rational.Create(1, 3)));
            Assert.That(Parse("integral(sin(x), x, 0, pi)").InnerSimplified, Is.EqualTo(Entity.Number.Integer.Create(2)));
            Assert.That(Parse("integral(ln(x), x, 0, 1)").InnerSimplified, Is.EqualTo(Entity.Number.Integer.Create(-1)), "An integrable singularity at a bound.");
            Assert.That(Parse("integral(1/(1+x^2), x, 0, +oo)").InnerSimplified.Simplify().ToString(), Is.EqualTo("pi / 2"));
            Assert.That(Parse("integral(1/x, x, -1, 1)").InnerSimplified.ToString(), Is.EqualTo("NaN"), "It diverges across the pole.");
            Assert.That(Parse("integral(x^3/(e^x-1), x, 0, +oo)").InnerSimplified, Is.TypeOf<Entity.Integralf>(), "No closed form is found for pi^4/15.");
            Assert.That(Parse("integral(x^3/(e^x-1), x, 0, +oo)").Evaled.ToString(), Does.Contain("2.718281828459045"), "Evaled replaces the constants with decimals, even inside an integral it cannot take.");
        }
    }

    [Test]
    public void ALimitIsNaNWhereItDoesNotExist()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Parse("sin(x)/x").Limit(X, 0), Is.EqualTo(Entity.Number.Integer.Create(1)));
            Assert.That(Parse("1/x").Limit(X, 0, ApproachFrom.Right).ToString(), Is.EqualTo("+oo"));
            Assert.That(Parse("1/x").Limit(X, 0, ApproachFrom.Left).ToString(), Is.EqualTo("-oo"));
            Assert.That(Parse("1/x").Limit(X, 0, ApproachFrom.BothSides).ToString(), Is.EqualTo("NaN"));
            Assert.That(Parse("(1 + 1/x)^x").Limit(X, Parse("+oo")), Is.TypeOf<Entity.Constant>());
            Assert.That(Parse("gamma(x)*sin(x)^x").Limit(X, 0), Is.TypeOf<Entity.Limitf>(), "A limit it cannot take comes back as a limit.");
        }
    }

    [Test]
    public void SolveWorksOverTheComplexNumbers()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Parse("x^2 - 5*x + 6 = 0").Solve(X).ToString(), Is.EqualTo("{ 2, 3 }"));
            Assert.That(Parse("x^2 + 1 = 0").Solve(X).ToString(), Is.EqualTo("{ i, -i }"));
            using (MathS.Settings.Codomain.Set(Domain.Real))
                Assert.That(Parse("x^2 + 1 = 0").Solve(X).ToString(), Is.EqualTo("{ i, -i }"), "The codomain does not restrict the solutions.");

            Assert.That(Parse("x^2 - 2 = 0").Solve(X).ToString(), Is.EqualTo("{ sqrt(2), -sqrt(2) }"), "The solutions stay exact.");
            Assert.That(Parse("sin(x) = 1/2").Solve(X).ToString(), Is.EqualTo("{ pi / 6 + 2 * pi * n_1, pi - pi / 6 + 2 * pi * n_1 }"), "n_1 stands for any integer.");
            Assert.That(Parse("x^2 < 4").Solve(X), Is.TypeOf<Entity.Set.Interval>());
            Assert.That(Parse("x + 1 = x").Solve(X).ToString(), Is.EqualTo("{  }"));
            Assert.Throws<SolveRequiresStatementException>(() => Parse("x^2 - 4").Solve(X));
        }
    }

    [Test]
    public void WithoutNewtonSolveAnswersOnlyInClosedForm()
    {
        var withNewton = (Entity.Set.FiniteSet)Parse("x^5 + 3*x + 1 = 0").Solve(X);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(withNewton.Count, Is.EqualTo(5));
            Assert.That(withNewton.All(root => root is Entity.Number.Complex { IsExact: false }), "Newton's roots are doubles, printed with every binary digit.");

            using (MathS.Settings.AllowNewton.Set(false))
                Assert.That(Parse("x^5 + 3*x + 1 = 0").Solve(X), Is.TypeOf<Entity.Set.ConditionalSet>());

            Assert.That(Parse("x^200 + x + 1 = 0").Solve(X), Is.TypeOf<Entity.Set.ConditionalSet>());
        }
    }

    [Test]
    public void SolveHonorsTheCancellationTokenOfItsThread()
    {
        var task = Task.Run(() =>
        {
            using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            MathS.Multithreading.SetLocalCancellationToken(source.Token);
            return Parse("x^20 + x + 1 = 0").Solve(X);
        });

        var exception = Assert.Throws<AggregateException>(() => task.Wait(TimeSpan.FromSeconds(10)))!;
        Assert.That(exception.InnerException, Is.InstanceOf<OperationCanceledException>(), "Without the token, this takes about 1.6 seconds.");
    }

    [Test]
    public void DeepNestingNeedsALargeStack()
    {
        // About 500 nested parentheses overflow the stack of a thread pool thread, which ends the process.
        // A thread of its own with 64 MB reads the deepest nesting a 2,000-character expression can hold:
        Entity? result = null;
        var thread = new Thread(() => result = Parse(new string('(', 1000) + "1" + new string(')', 1000)).Evaled, 64 * 1024 * 1024);
        thread.Start();
        thread.Join();

        Assert.That(result, Is.EqualTo(Entity.Number.Integer.Create(1)));
    }
}