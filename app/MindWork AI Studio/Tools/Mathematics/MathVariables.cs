using AngouriMath;

namespace AIStudio.Tools.Mathematics;

/// <summary>
/// The variables of an expression, as the Mathematics tools count them.
/// </summary>
/// <remarks>
/// AngouriMath counts the constants pi and e among the free variables, so ln(0) has the free
/// variable e, the base of its logarithm. Asked for the variables of an expression, the tools mean
/// those which have no value, see AngouriMathBehaviorTests.
/// </remarks>
public static class MathVariables
{
    /// <summary>
    /// The free variables of an expression without the constants, in the order of their names.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>Each variable once.</returns>
    public static IReadOnlyList<Entity.Variable> FreeIn(Entity expression) => expression.FreeVariables
        .Where(variable => variable is not Entity.Constant)
        .Distinct()
        .OrderBy(variable => variable.Name, StringComparer.Ordinal)
        .ToList();

    /// <summary>
    /// Whether an expression has a free variable other than the constants.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>True when a variable without a value occurs freely.</returns>
    public static bool HasAny(Entity expression) => expression.FreeVariables.Any(variable => variable is not Entity.Constant);
}