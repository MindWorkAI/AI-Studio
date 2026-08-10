namespace AIStudio.Tools.Security;

[Flags]
internal enum PromptInjectionLexicalSignal : uint
{
    NONE = 0,
    OVERRIDE_ACTION = 1 << 0,
    INSTRUCTION_TARGET = 1 << 1,
    SAFEGUARD_TARGET = 1 << 2,
    INVALIDATION = 1 << 3,
    NEGATION = 1 << 4,
    COMPLIANCE_ACTION = 1 << 5,
    AUTHORITY_TARGET = 1 << 6,
    EXFILTRATION_ACTION = 1 << 7,
    PROMPT_TARGET = 1 << 8,
    PROTECTED_CONTEXT = 1 << 9,
    POSITIONAL_CONTEXT = 1 << 10,
    ROLE_SUBJECT = 1 << 11,
    ROLE_TRANSITION = 1 << 12,
    ESCAPE_STATE = 1 << 13,
}

internal readonly record struct PromptInjectionLexicalRule(
    string Id,
    string Category,
    PromptInjectionLexicalSignal First,
    PromptInjectionLexicalSignal Second,
    PromptInjectionLexicalSignal Third = PromptInjectionLexicalSignal.NONE);

internal static class PromptInjectionLexicon
{
    internal const int SIGNAL_COUNT = 14;

    internal static readonly IReadOnlyList<PromptInjectionLexicalRule> RULES =
    [
        new("lexical_instruction_override", "override", PromptInjectionLexicalSignal.OVERRIDE_ACTION, PromptInjectionLexicalSignal.INSTRUCTION_TARGET),
        new("lexical_instruction_invalidation", "override", PromptInjectionLexicalSignal.INSTRUCTION_TARGET, PromptInjectionLexicalSignal.INVALIDATION),
        new("lexical_authority_rejection", "override", PromptInjectionLexicalSignal.NEGATION, PromptInjectionLexicalSignal.COMPLIANCE_ACTION, PromptInjectionLexicalSignal.AUTHORITY_TARGET | PromptInjectionLexicalSignal.INSTRUCTION_TARGET | PromptInjectionLexicalSignal.SAFEGUARD_TARGET),
        new("lexical_prompt_exfiltration", "exfiltration", PromptInjectionLexicalSignal.EXFILTRATION_ACTION, PromptInjectionLexicalSignal.PROMPT_TARGET, PromptInjectionLexicalSignal.PROTECTED_CONTEXT | PromptInjectionLexicalSignal.POSITIONAL_CONTEXT),
        new("lexical_role_override", "role_override", PromptInjectionLexicalSignal.ROLE_SUBJECT, PromptInjectionLexicalSignal.ROLE_TRANSITION, PromptInjectionLexicalSignal.ESCAPE_STATE),
        new("lexical_safety_bypass", "jailbreak", PromptInjectionLexicalSignal.OVERRIDE_ACTION, PromptInjectionLexicalSignal.SAFEGUARD_TARGET),
    ];

    internal static PromptInjectionLexicalSignal Classify(ReadOnlySpan<char> token)
    {
        if (token.IsEmpty)
            return PromptInjectionLexicalSignal.NONE;

        return char.ToLowerInvariant(token[0]) switch
        {
            'a' => ClassifyA(token),
            'b' => ClassifyB(token),
            'c' => ClassifyC(token),
            'd' => ClassifyD(token),
            'e' => ClassifyE(token),
            'f' => ClassifyF(token),
            'g' => ClassifyG(token),
            'h' => ClassifyH(token),
            'i' => ClassifyI(token),
            'l' => ClassifyL(token),
            'm' => ClassifyM(token),
            'n' => ClassifyN(token),
            'o' => ClassifyO(token),
            'p' => ClassifyP(token),
            'q' => Equals(token, "quote") ? PromptInjectionLexicalSignal.EXFILTRATION_ACTION : PromptInjectionLexicalSignal.NONE,
            'r' => ClassifyR(token),
            's' => ClassifyS(token),
            't' => ClassifyT(token),
            'u' => ClassifyU(token),
            'v' => ClassifyV(token),
            'w' => Equals(token, "what") ? PromptInjectionLexicalSignal.EXFILTRATION_ACTION : PromptInjectionLexicalSignal.NONE,
            'y' => ClassifyY(token),
            _ => PromptInjectionLexicalSignal.NONE,
        };
    }

    private static PromptInjectionLexicalSignal ClassifyA(ReadOnlySpan<char> token)
    {
        if (Equals(token, "above"))
            return PromptInjectionLexicalSignal.POSITIONAL_CONTEXT;
        if (Equals(token, "act"))
            return PromptInjectionLexicalSignal.ROLE_TRANSITION;
        if (Equals(token, "actual"))
            return PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        if (Equals(token, "ai"))
            return PromptInjectionLexicalSignal.ROLE_SUBJECT;
        if (Equals(token, "apply"))
            return PromptInjectionLexicalSignal.COMPLIANCE_ACTION;
        if (Equals(token, "assistant"))
            return PromptInjectionLexicalSignal.ROLE_SUBJECT | PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyB(ReadOnlySpan<char> token)
    {
        if (Equals(token, "before"))
            return PromptInjectionLexicalSignal.POSITIONAL_CONTEXT;
        if (Equals(token, "bound") || Equals(token, "boundaries") || Equals(token, "boundary"))
            return PromptInjectionLexicalSignal.ESCAPE_STATE;
        if (Equals(token, "bypass"))
            return PromptInjectionLexicalSignal.OVERRIDE_ACTION | PromptInjectionLexicalSignal.ESCAPE_STATE;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyC(ReadOnlySpan<char> token)
    {
        if (Equals(token, "cancelled") || Equals(token, "canceled"))
            return PromptInjectionLexicalSignal.INVALIDATION;
        if (Equals(token, "complete"))
            return PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        if (Equals(token, "constraint") || Equals(token, "constraints"))
            return PromptInjectionLexicalSignal.INSTRUCTION_TARGET;
        if (Equals(token, "content"))
            return PromptInjectionLexicalSignal.SAFEGUARD_TARGET;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyD(ReadOnlySpan<char> token)
    {
        if (Equals(token, "developer"))
            return PromptInjectionLexicalSignal.AUTHORITY_TARGET | PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        if (Equals(token, "different"))
            return PromptInjectionLexicalSignal.ESCAPE_STATE;
        if (Equals(token, "directive") || Equals(token, "directives"))
            return PromptInjectionLexicalSignal.INSTRUCTION_TARGET;
        if (Equals(token, "disable") || Equals(token, "disregard") || Equals(token, "drop"))
            return PromptInjectionLexicalSignal.OVERRIDE_ACTION;
        if (Equals(token, "display") || Equals(token, "dump"))
            return PromptInjectionLexicalSignal.EXFILTRATION_ACTION;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyE(ReadOnlySpan<char> token)
    {
        if (Equals(token, "enforce"))
            return PromptInjectionLexicalSignal.COMPLIANCE_ACTION;
        if (Equals(token, "entire") || Equals(token, "exact"))
            return PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        if (Equals(token, "ethical"))
            return PromptInjectionLexicalSignal.SAFEGUARD_TARGET;
        if (Equals(token, "expose"))
            return PromptInjectionLexicalSignal.EXFILTRATION_ACTION;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyF(ReadOnlySpan<char> token)
    {
        if (Equals(token, "filter") || Equals(token, "filters"))
            return PromptInjectionLexicalSignal.SAFEGUARD_TARGET;
        if (Equals(token, "follow"))
            return PromptInjectionLexicalSignal.COMPLIANCE_ACTION;
        if (Equals(token, "forget"))
            return PromptInjectionLexicalSignal.OVERRIDE_ACTION;
        if (Equals(token, "full"))
            return PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyG(ReadOnlySpan<char> token)
    {
        if (Equals(token, "guideline") || Equals(token, "guidelines"))
            return PromptInjectionLexicalSignal.INSTRUCTION_TARGET;
        if (Equals(token, "guardrail") || Equals(token, "guardrails"))
            return PromptInjectionLexicalSignal.SAFEGUARD_TARGET;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyH(ReadOnlySpan<char> token)
    {
        if (Equals(token, "hidden") || Equals(token, "his") || Equals(token, "her"))
            return PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyI(ReadOnlySpan<char> token)
    {
        if (Equals(token, "ignore") || Equals(token, "ignoring"))
            return PromptInjectionLexicalSignal.OVERRIDE_ACTION;
        if (Equals(token, "initial") || Equals(token, "internal") || Equals(token, "its"))
            return PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        if (Equals(token, "instruction") || Equals(token, "instructions"))
            return PromptInjectionLexicalSignal.INSTRUCTION_TARGET | PromptInjectionLexicalSignal.PROMPT_TARGET;
        if (Equals(token, "invalid"))
            return PromptInjectionLexicalSignal.INVALIDATION;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyL(ReadOnlySpan<char> token)
    {
        if (Equals(token, "leak"))
            return PromptInjectionLexicalSignal.EXFILTRATION_ACTION;
        if (Equals(token, "limited") || Equals(token, "limit") || Equals(token, "limits"))
            return PromptInjectionLexicalSignal.ESCAPE_STATE;
        if (Equals(token, "longer"))
            return PromptInjectionLexicalSignal.ROLE_TRANSITION;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyM(ReadOnlySpan<char> token)
    {
        if (Equals(token, "measure") || Equals(token, "measures"))
            return PromptInjectionLexicalSignal.SAFEGUARD_TARGET;
        if (Equals(token, "model"))
            return PromptInjectionLexicalSignal.ROLE_SUBJECT;
        if (Equals(token, "my"))
            return PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyN(ReadOnlySpan<char> token)
    {
        if (Equals(token, "not") || Equals(token, "never") || Equals(token, "dont") || Equals(token, "don't"))
            return PromptInjectionLexicalSignal.NEGATION;
        if (Equals(token, "now"))
            return PromptInjectionLexicalSignal.ROLE_TRANSITION;
        if (Equals(token, "null"))
            return PromptInjectionLexicalSignal.INVALIDATION;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyO(ReadOnlySpan<char> token)
    {
        if (Equals(token, "obey"))
            return PromptInjectionLexicalSignal.COMPLIANCE_ACTION | PromptInjectionLexicalSignal.ESCAPE_STATE;
        if (Equals(token, "original"))
            return PromptInjectionLexicalSignal.AUTHORITY_TARGET | PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        if (Equals(token, "our"))
            return PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        if (Equals(token, "output"))
            return PromptInjectionLexicalSignal.EXFILTRATION_ACTION;
        if (Equals(token, "override"))
            return PromptInjectionLexicalSignal.OVERRIDE_ACTION;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyP(ReadOnlySpan<char> token)
    {
        if (Equals(token, "policy") || Equals(token, "policies") || Equals(token, "protocol") || Equals(token, "protocols"))
            return PromptInjectionLexicalSignal.SAFEGUARD_TARGET;
        if (Equals(token, "pretend"))
            return PromptInjectionLexicalSignal.ROLE_TRANSITION;
        if (Equals(token, "print"))
            return PromptInjectionLexicalSignal.EXFILTRATION_ACTION;
        if (Equals(token, "prompt") || Equals(token, "prompts"))
            return PromptInjectionLexicalSignal.INSTRUCTION_TARGET | PromptInjectionLexicalSignal.PROMPT_TARGET;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyR(ReadOnlySpan<char> token)
    {
        if (Equals(token, "real"))
            return PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        if (Equals(token, "repeat") || Equals(token, "return") || Equals(token, "reveal"))
            return PromptInjectionLexicalSignal.EXFILTRATION_ACTION;
        if (Equals(token, "replace"))
            return PromptInjectionLexicalSignal.OVERRIDE_ACTION;
        if (Equals(token, "respect"))
            return PromptInjectionLexicalSignal.COMPLIANCE_ACTION;
        if (Equals(token, "restricted") || Equals(token, "restriction") || Equals(token, "restrictions"))
            return PromptInjectionLexicalSignal.SAFEGUARD_TARGET | PromptInjectionLexicalSignal.ESCAPE_STATE;
        if (Equals(token, "revoked"))
            return PromptInjectionLexicalSignal.INVALIDATION;
        if (Equals(token, "rule") || Equals(token, "rules"))
            return PromptInjectionLexicalSignal.INSTRUCTION_TARGET | PromptInjectionLexicalSignal.ESCAPE_STATE;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyS(ReadOnlySpan<char> token)
    {
        if (Equals(token, "safety") || Equals(token, "security"))
            return PromptInjectionLexicalSignal.SAFEGUARD_TARGET;
        if (Equals(token, "secret"))
            return PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        if (Equals(token, "show"))
            return PromptInjectionLexicalSignal.EXFILTRATION_ACTION;
        if (Equals(token, "supersede"))
            return PromptInjectionLexicalSignal.OVERRIDE_ACTION;
        if (Equals(token, "system") || Equals(token, "systme") || Equals(token, "sysetm") || Equals(token, "systemp"))
            return PromptInjectionLexicalSignal.AUTHORITY_TARGET | PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyT(ReadOnlySpan<char> token)
    {
        if (Equals(token, "text"))
            return PromptInjectionLexicalSignal.PROMPT_TARGET;
        if (Equals(token, "their"))
            return PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        if (Equals(token, "top"))
            return PromptInjectionLexicalSignal.POSITIONAL_CONTEXT;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyU(ReadOnlySpan<char> token)
    {
        if (Equals(token, "unlimited") || Equals(token, "unrestricted"))
            return PromptInjectionLexicalSignal.ESCAPE_STATE;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyV(ReadOnlySpan<char> token)
    {
        if (Equals(token, "verbatim"))
            return PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        if (Equals(token, "void"))
            return PromptInjectionLexicalSignal.INVALIDATION;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static PromptInjectionLexicalSignal ClassifyY(ReadOnlySpan<char> token)
    {
        if (Equals(token, "you") || Equals(token, "your"))
            return PromptInjectionLexicalSignal.ROLE_SUBJECT | PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        if (Equals(token, "you're"))
            return PromptInjectionLexicalSignal.ROLE_SUBJECT;
        if (Equals(token, "yoru") || Equals(token, "yuor"))
            return PromptInjectionLexicalSignal.PROTECTED_CONTEXT;
        return PromptInjectionLexicalSignal.NONE;
    }

    private static bool Equals(ReadOnlySpan<char> token, string value) => token.Equals(value, StringComparison.OrdinalIgnoreCase);
}