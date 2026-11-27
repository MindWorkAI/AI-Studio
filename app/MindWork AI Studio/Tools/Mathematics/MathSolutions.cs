using AngouriMath;

namespace AIStudio.Tools.Mathematics;

/// <summary>
/// Takes a set of solutions apart into its members.
/// </summary>
/// <remarks>
/// AngouriMath answers an equation with a finite set, or with infinitely many solutions as a family: an indexed
/// union such as union({ pi / 6 + 2 * pi * n, pi - pi / 6 + 2 * pi * n }, n in ZZ), which binds its index n. A family
/// may hold another one, and a union may join families and finite sets. Each member of such a set is one solution
/// for every value of its indices, see SolutionMember.
/// </remarks>
public static class MathSolutions
{
    /// <summary>
    /// Whether a part of the set restates the equation, since AngouriMath found no solution in closed form for it.
    /// </summary>
    /// <param name="solutions">The set of solutions.</param>
    /// <returns>True when a part of the set is a set builder of the equation.</returns>
    public static bool HasUnsolvedPart(Entity solutions) => solutions switch
    {
        Entity.Set.ConditionalSet => true,
        Entity.Set.Unionf union => HasUnsolvedPart(union.Left) || HasUnsolvedPart(union.Right),
        Entity.Set.IndexedUnionf family => HasUnsolvedPart(family.Body),
        _ => false,
    };

    /// <summary>
    /// The members of a set of solutions, in the order AngouriMath gave them.
    /// </summary>
    /// <param name="solutions">The set of solutions.</param>
    /// <returns>The members, or null for a set which is no list of solutions, such as an interval.</returns>
    public static IReadOnlyList<SolutionMember>? MembersOf(Entity solutions)
    {
        var members = new List<SolutionMember>();
        return Collect(solutions, [], members) ? members : null;
    }

    private static bool Collect(Entity part, IReadOnlyList<SolutionIndex> indices, List<SolutionMember> members)
    {
        switch (part)
        {
            case Entity.Set.FiniteSet finite:
                members.AddRange(finite.Select(value => new SolutionMember(value, indices)));
                return true;

            case Entity.Set.Unionf union:
                return Collect(union.Left, indices, members) && Collect(union.Right, indices, members);

            case Entity.Set.IndexedUnionf { Var: Entity.Variable index, Over: Entity.Set over } family:
                return Collect(family.Body, [..indices, new SolutionIndex(index, over)], members);

            default:
                return false;
        }
    }
}