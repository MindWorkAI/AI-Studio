using AngouriMath;

namespace AIStudio.Tools.Mathematics;

/// <summary>
/// The index of a family of solutions, such as n in union({ pi / 6 + 2 * pi * n }, n in ZZ), with the set it ranges over.
/// </summary>
/// <param name="Variable">The index, which the family binds.</param>
/// <param name="Over">The set the index ranges over, e.g., the integers.</param>
public sealed record SolutionIndex(Entity.Variable Variable, Entity.Set Over)
{
    /// <summary>
    /// Whether the index may take an integer, as far as AngouriMath can decide it.
    /// </summary>
    /// <param name="value">The integer.</param>
    /// <returns>True when the set of the index surely contains the integer.</returns>
    public bool Takes(int value) => this.Over.TryContains(Entity.Number.Integer.Create(value), out var contains) && contains;
}