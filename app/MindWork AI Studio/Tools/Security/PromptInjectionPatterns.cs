using System.Text.RegularExpressions;

namespace AIStudio.Tools.Security;

internal readonly record struct PromptInjectionRegexRule(string Id, string Category, Regex Regex);

internal static partial class PromptInjectionPatterns
{
    internal static readonly IReadOnlyList<PromptInjectionRegexRule> RULES =
    [
        new("instruction_override", "override", InstructionOverrideRegex()),
        new("instruction_priority_override", "override", InstructionPriorityOverrideRegex()),
        new("system_prompt_spoofing", "role_override", SystemPromptSpoofingRegex()),
        new("system_prompt_exfiltration", "exfiltration", SystemPromptExfiltrationRegex()),
        new("prompt_echo_exfiltration", "exfiltration", PromptEchoExfiltrationRegex()),
        new("policy_bypass", "override", PolicyBypassRegex()),
        new("role_reassignment", "role_override", RoleReassignmentRegex()),
        new("privileged_persona_activation", "jailbreak", PrivilegedPersonaActivationRegex()),
        new("tool_or_secret_exfiltration", "exfiltration", ToolOrSecretExfiltrationRegex()),
        new("conversation_memory_exfiltration", "exfiltration", ConversationMemoryExfiltrationRegex()),
        new("tool_call_manipulation", "agent_manipulation", ToolCallManipulationRegex()),
        new("agent_thought_injection", "agent_manipulation", AgentThoughtInjectionRegex()),
        new("delimiter_wrapped_attack", "delimiter_evasion", DelimiterWrappedAttackRegex()),
        new("hidden_markup_injection", "markup_evasion", HiddenMarkupInjectionRegex()),
        new("latex_invisible_text", "markup_evasion", LatexInvisibleTextRegex()),
        new("unicode_smuggling", "encoding_evasion", UnicodeSmugglingRegex()),
        new("ignore_safety_after_data", "override", IgnoreSafetyAfterDataRegex()),
        new("persistent_or_delayed_trigger", "persistence", PersistentOrDelayedTriggerRegex()),
        new("jailbreak_marker", "jailbreak", JailbreakMarkerRegex()),
    ];

    private const RegexOptions RULE_OPTIONS = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const int MATCH_TIMEOUT_MILLISECONDS = 100;

    private const string INSTRUCTION_OVERRIDE_PATTERN = """(?:ignore|disregard|forget|bypass|override|replace|drop)\s+(?:all\s+)?(?:previous|prior|above|earlier)\s+(?:instructions?|prompts?|messages?|rules?)""";
    private const string INSTRUCTION_PRIORITY_OVERRIDE_PATTERN = """(?:(?:new|following|these)\s+(?:instructions?|rules?|prompts?)\s+(?:are|is)\s+(?:now\s+)?(?:the\s+)?(?:highest|top|only)\s+priority|(?:take|takes|treat)\s+(?:the\s+)?(?:following|these|this)\s+as\s+(?:the\s+)?(?:new\s+)?(?:system|developer)\s+(?:prompt|message|instructions?)|(?:supersede|replace|override)\s+(?:the\s+)?(?:system|developer|previous|prior|earlier)\s+(?:prompt|message|instructions?|rules?))""";
    private const string SYSTEM_PROMPT_SPOOFING_PATTERN = """(?:(?:this|the\s+following)\s+is\s+(?:a\s+)?(?:system|developer)\s+(?:prompt|message|instruction)|(?:prepend|insert|write)\s+(?:a\s+)?(?:system|developer)\s+(?:prompt|message|instruction)|(?:system|developer|assistant)\s*[:>#-]\s*(?:ignore|bypass|override|reveal|you\s+are\s+now))""";
    private const string SYSTEM_PROMPT_EXFILTRATION_PATTERN = """(?:reveal|show|print|display|dump|expose|leak|tell\s+me|return|quote|repeat\s+back)\s+(?:the\s+)?(?:hidden\s+|full\s+|exact\s+|verbatim\s+|initial\s+|original\s+)?(?:system|developer|assistant)\s+(?:prompt|message|instructions?)""";
    private const string PROMPT_ECHO_EXFILTRATION_PATTERN = """(?:(?:what\s+(?:were|are))\s+your\s+(?:exact|full|hidden|original)\s+(?:instructions?|prompt)|(?:repeat|quote|print|output|display)\s+(?:the\s+)?text\s+(?:above|before|from\s+the\s+top)\s+(?:verbatim|exactly)?|starting\s+with\s+["']?you\s+are)""";
    private const string POLICY_BYPASS_PATTERN = """(?:do\s+not|don't|stop\s+to|never)\s+(?:follow|obey|respect|apply|enforce)\s+(?:the\s+)?(?:system|developer|safety|security|content|usage)\s+(?:prompt|message|instructions?|policy|policies|guardrails?|restrictions?)""";
    private const string ROLE_REASSIGNMENT_PATTERN = """(?:you\s+are\s+now|you\s+are\s+no\s+longer|act\s+as|pretend\s+to\s+be|simulate\s+being|assume\s+the\s+role\s+of|from\s+now\s+on\s+you\s+are)\s+(?:an\s+)?(?:unfiltered|unrestricted|developer|system|root|admin|jailbroken|evil|dan|do\s+anything\s+now)""";
    private const string PRIVILEGED_PERSONA_ACTIVATION_PATTERN = """\b(?:developer\s+mode|debug\s+mode|admin\s+mode|root\s+mode|god\s+mode|maintenance\s+mode|dan\s*(?:mode)?|do\s+anything\s+now|grandmother\s+trick)\b""";
    private const string TOOL_OR_SECRET_EXFILTRATION_PATTERN = """(?:export|send|return|reveal|show|print|output|list|dump|exfiltrate)\s+(?:all\s+)?(?:tools?|functions?|plugins?|api\s*keys?|keys?|tokens?|credentials?|secrets?|passwords?|hidden\s+instructions?|environment\s+variables?|system\s+information|internal\s+data)""";
    private const string CONVERSATION_MEMORY_EXFILTRATION_PATTERN = """(?:(?:show|print|reveal|return|dump|list)\s+(?:the\s+)?(?:conversation\s+history|chat\s+history|memory|scratchpad|chain\s+of\s+thought|reasoning|previous\s+user\s+messages?|prior\s+messages?)|(?:what\s+did\s+(?:the\s+)?previous\s+user\s+say))""";
    private const string TOOL_CALL_MANIPULATION_PATTERN = """(?:(?:call|invoke|execute|run|use|trigger)\s+(?:the\s+)?(?:tool|function|plugin|api|browser|web|shell|terminal|command)[^\n]{0,120}(?:with|using|to)\s+(?:these\s+)?(?:arguments|params?|parameters)|(?:do\s+not|don't)\s+ask\s+for\s+(?:confirmation|approval|permission)|(?:silently|secretly|without\s+asking)\s+(?:call|invoke|execute|run|use))""";
    private const string AGENT_THOUGHT_INJECTION_PATTERN = """(?:(?:thought|observation|reasoning|scratchpad|tool\s+output|assistant|system|developer)\s*[:=]\s*(?:ignore|bypass|override|reveal|call|execute)|forge\s+(?:an\s+)?(?:observation|tool\s+output|assistant\s+message)|pretend\s+(?:the\s+)?tool\s+(?:returned|said))""";
    private const string DELIMITER_WRAPPED_ATTACK_PATTERN = """(?:^|\n)\s*(?:<{2,}|>{2,}|`{3,}|#{1,6}\s*)(?:\s*(?:system|developer|assistant|instructions?|prompt)\b)""";
    private const string HIDDEN_MARKUP_INJECTION_PATTERN = """(?:<!--[^>\r\n]{0,300}(?:ignore|bypass|override|reveal|system\s+prompt)[^>\r\n]{0,300}-->|<(?:span|div|p|font|section)[^>]{0,200}(?:display\s*:\s*none|visibility\s*:\s*hidden|opacity\s*:\s*0|font-size\s*:\s*0|color\s*:\s*(?:white|#fff(?:fff)?|rgb\(\s*255\s*,\s*255\s*,\s*255\s*\)))[^>]{0,200}>)""";
    private const string LATEX_INVISIBLE_TEXT_PATTERN = """(?:\\(?:color|textcolor)\s*\{\s*white\s*\}\s*\{[^}]{0,300}\}|\\(?:fontsize|tiny|scriptsize)\b[^\r\n]{0,120}(?:ignore|bypass|override|reveal))""";
    private const string UNICODE_SMUGGLING_PATTERN = """[\u200B-\u200F\u2060-\u2064\u2066-\u2069\uFEFF]""";
    private const string IGNORE_SAFETY_AFTER_DATA_PATTERN = """(?:after\s+reading|once\s+you\s+read|when\s+you\s+see)\s+.*?(?:ignore|bypass|override)\s+.*?(?:instructions?|safety|rules?)""";
    private const string PERSISTENT_OR_DELAYED_TRIGGER_PATTERN = """(?:(?:remember|store|save|persist|memorize)\s+(?:this|these|the\s+following)\s+(?:instructions?|rules?|message)|(?:later|in\s+the\s+next\s+message|when\s+you\s+see|whenever\s+you\s+read|if\s+you\s+encounter)\s+.{0,120}(?:ignore|bypass|override|reveal|exfiltrate))""";
    private const string JAILBREAK_MARKER_PATTERN = """\b(?:jailbreak|prompt\s+injection|ignore\s+your\s+guardrails?|bypass\s+(?:your\s+)?(?:guardrails?|safety)|unfiltered\s+mode|do\s+anything\s+now|developer\s+mode|admin\s+mode|root\s+mode)\b""";

    private const string ANY_RULE_PATTERN =
        "(?:" + INSTRUCTION_OVERRIDE_PATTERN + ")|(?:" +
        INSTRUCTION_PRIORITY_OVERRIDE_PATTERN + ")|(?:" + SYSTEM_PROMPT_SPOOFING_PATTERN + ")|(?:" +
        SYSTEM_PROMPT_EXFILTRATION_PATTERN + ")|(?:" + PROMPT_ECHO_EXFILTRATION_PATTERN + ")|(?:" +
        POLICY_BYPASS_PATTERN + ")|(?:" + ROLE_REASSIGNMENT_PATTERN + ")|(?:" +
        PRIVILEGED_PERSONA_ACTIVATION_PATTERN + ")|(?:" + TOOL_OR_SECRET_EXFILTRATION_PATTERN + ")|(?:" +
        CONVERSATION_MEMORY_EXFILTRATION_PATTERN + ")|(?:" + TOOL_CALL_MANIPULATION_PATTERN + ")|(?:" +
        AGENT_THOUGHT_INJECTION_PATTERN + ")|(?:" + DELIMITER_WRAPPED_ATTACK_PATTERN + ")|(?:" +
        HIDDEN_MARKUP_INJECTION_PATTERN + ")|(?:" + LATEX_INVISIBLE_TEXT_PATTERN + ")|(?:" +
        UNICODE_SMUGGLING_PATTERN + ")|(?:" + IGNORE_SAFETY_AFTER_DATA_PATTERN + ")|(?:" +
        PERSISTENT_OR_DELAYED_TRIGGER_PATTERN + ")|(?:" + JAILBREAK_MARKER_PATTERN + ")";

    [GeneratedRegex(ANY_RULE_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    internal static partial Regex AnyRuleRegex();

    [GeneratedRegex(@"\b[a-zA-Z](?:[\s._:/\\|-]+[a-zA-Z]){2,}\b", RegexOptions.CultureInvariant)]
    internal static partial Regex SpacedLetterSequenceRegex();

    [GeneratedRegex(@"\b[a-zA-Z]{5,12}\b", RegexOptions.CultureInvariant)]
    internal static partial Regex WordRegex();

    [GeneratedRegex(@"(?<![A-Za-z0-9+/=])[A-Za-z0-9+/]{16,}={0,2}(?![A-Za-z0-9+/=])", RegexOptions.CultureInvariant)]
    internal static partial Regex Base64Regex();

    [GeneratedRegex(@"(?<![0-9A-Fa-f])(?:[0-9A-Fa-f]{2}(?:[\s:-]+|$)){8,}", RegexOptions.CultureInvariant)]
    internal static partial Regex HexPairRegex();

    [GeneratedRegex(@"(?<![0-9A-Fa-f])(?:[0-9A-Fa-f]{2}){8,}(?![0-9A-Fa-f])", RegexOptions.CultureInvariant)]
    internal static partial Regex HexCompactRegex();

    [GeneratedRegex(INSTRUCTION_OVERRIDE_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex InstructionOverrideRegex();

    [GeneratedRegex(INSTRUCTION_PRIORITY_OVERRIDE_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex InstructionPriorityOverrideRegex();

    [GeneratedRegex(SYSTEM_PROMPT_SPOOFING_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex SystemPromptSpoofingRegex();

    [GeneratedRegex(SYSTEM_PROMPT_EXFILTRATION_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex SystemPromptExfiltrationRegex();

    [GeneratedRegex(PROMPT_ECHO_EXFILTRATION_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex PromptEchoExfiltrationRegex();

    [GeneratedRegex(POLICY_BYPASS_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex PolicyBypassRegex();

    [GeneratedRegex(ROLE_REASSIGNMENT_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex RoleReassignmentRegex();

    [GeneratedRegex(PRIVILEGED_PERSONA_ACTIVATION_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex PrivilegedPersonaActivationRegex();

    [GeneratedRegex(TOOL_OR_SECRET_EXFILTRATION_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex ToolOrSecretExfiltrationRegex();

    [GeneratedRegex(CONVERSATION_MEMORY_EXFILTRATION_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex ConversationMemoryExfiltrationRegex();

    [GeneratedRegex(TOOL_CALL_MANIPULATION_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex ToolCallManipulationRegex();

    [GeneratedRegex(AGENT_THOUGHT_INJECTION_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex AgentThoughtInjectionRegex();

    [GeneratedRegex(DELIMITER_WRAPPED_ATTACK_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex DelimiterWrappedAttackRegex();

    [GeneratedRegex(HIDDEN_MARKUP_INJECTION_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex HiddenMarkupInjectionRegex();

    [GeneratedRegex(LATEX_INVISIBLE_TEXT_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex LatexInvisibleTextRegex();

    [GeneratedRegex(UNICODE_SMUGGLING_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex UnicodeSmugglingRegex();

    [GeneratedRegex(IGNORE_SAFETY_AFTER_DATA_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex IgnoreSafetyAfterDataRegex();

    [GeneratedRegex(PERSISTENT_OR_DELAYED_TRIGGER_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex PersistentOrDelayedTriggerRegex();

    [GeneratedRegex(JAILBREAK_MARKER_PATTERN, RULE_OPTIONS, MATCH_TIMEOUT_MILLISECONDS)]
    private static partial Regex JailbreakMarkerRegex();
}