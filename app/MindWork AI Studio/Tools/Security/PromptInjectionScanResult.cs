namespace AIStudio.Tools.Security;

public sealed class PromptInjectionScanResult(PromptInjectionSource source, IReadOnlyList<PromptInjectionFinding> findings)
{
    public PromptInjectionSource Source { get; } = source;

    public IReadOnlyList<PromptInjectionFinding> Findings { get; } = findings;

    public bool IsBlocked => this.Findings.Count > 0;

    public IReadOnlyList<string> RuleIds => this.Findings.Select(finding => finding.RuleId).Distinct(StringComparer.Ordinal).ToList();
}