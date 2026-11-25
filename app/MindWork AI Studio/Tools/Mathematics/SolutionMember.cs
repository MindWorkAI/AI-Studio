using AngouriMath;

namespace AIStudio.Tools.Mathematics;

/// <summary>
/// One member of a set of solutions: a single solution, or a solution for every value of its indices.
/// </summary>
/// <param name="Value">The solution, in which the indices occur freely.</param>
/// <param name="Indices">The indices of the families which hold the solution, from the outermost one; empty for a single solution.</param>
public sealed record SolutionMember(Entity Value, IReadOnlyList<SolutionIndex> Indices);