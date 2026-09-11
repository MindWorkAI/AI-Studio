namespace AIStudio.Models.Matching;

/// <summary>
/// Two rules which claim the same names with the same right.
/// </summary>
/// <param name="First">One of the two rules.</param>
/// <param name="Second">The other one.</param>
/// <param name="Reason">What makes them collide, in a sentence a person can act on.</param>
public sealed record RuleAmbiguity(ModelRule First, ModelRule Second, string Reason)
{
    public override string ToString() => $"{this.Reason} ({this.First.Description} <-> {this.Second.Description})";
}