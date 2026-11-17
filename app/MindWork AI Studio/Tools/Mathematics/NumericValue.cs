using AngouriMath;

namespace AIStudio.Tools.Mathematics;

/// <summary>
/// The value of an expression without variables, with as many digits as the computation vouches for.
/// </summary>
/// <param name="Exact">The exact value when it is rational, or complex with rational parts; otherwise null.</param>
/// <param name="Value">The value at the working precision, before it is rounded to its digits.</param>
/// <param name="Digits">How many significant digits of the value are certain; zero when none is.</param>
/// <param name="Approximate">Whether a part of the value was integrated numerically.</param>
/// <param name="EstimatedError">The estimated absolute error of an approximate value; null for any other.</param>
/// <param name="Issues">What the model has to know about the value.</param>
public sealed record NumericValue(Entity.Number? Exact, Entity.Number.Complex Value, int Digits, bool Approximate, double? EstimatedError, IReadOnlyList<string> Issues);