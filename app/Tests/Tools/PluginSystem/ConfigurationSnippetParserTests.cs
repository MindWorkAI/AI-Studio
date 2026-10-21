using System.Globalization;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

using Lua;
using Lua.Standard;

using AuthMethod = AIStudio.Tools.ERIClient.DataModel.AuthMethod;
using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that the snippet parser reads what the app exports, reads it the way Lua does, and
/// refuses whatever is not plain data.
/// </summary>
/// <remarks>
/// The parser stands between a text somebody pasted and the settings of the app, so accepting a
/// snippet is not enough: every snippet it accepts is also run through a real Lua state, and both
/// tables are compared value by value. A parser which read a single string differently than Lua
/// would hand the import something the configuration plugin of the same snippet never contains.<br/><br/>
/// Nothing here asserts on the wording of an issue: I18N is process-wide state without a reset, so
/// an Init somewhere else would decide whether these pass.
/// </remarks>
[TestFixture]
public sealed class ConfigurationSnippetParserTests
{
    /// <summary>
    /// Quotes, backslashes, and apostrophes, which a quoted Lua string has to escape.
    /// </summary>
    private const string QUOTED_TEXT = "He said \"hi\" to C:\\Users\\me, and 'bye'.";

    /// <summary>
    /// A line break at the start, an empty line, an indentation, and the closing brackets of long
    /// strings, inside and at the end. Carriage returns are left to the output rules of the policy:
    /// a text with one is exported as a quoted string, so it would never reach a long string here.
    /// </summary>
    private const string MULTILINE_TEXT = "\nFirst line\nSecond line\n\n\tIndented, with ]] and ]==] and ]===] inside, and ending like one: ]===";

    private const string WINDOWS_PATH = @"C:\Users\me\AppData\Roaming\MindWork AI Studio\tokenizer.json";

    /// <summary>
    /// Every type of configuration object which configuration plugins provide, and thus can be exported.
    /// </summary>
    private static readonly PluginConfigurationObjectType[] EXPORTABLE_TYPES =
    [
        PluginConfigurationObjectType.LLM_PROVIDER,
        PluginConfigurationObjectType.EMBEDDING_PROVIDER,
        PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER,
        PluginConfigurationObjectType.PROFILE,
        PluginConfigurationObjectType.CHAT_TEMPLATE,
        PluginConfigurationObjectType.DATA_SOURCE,
        PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY,
    ];

    [TestCaseSource(nameof(EXPORTABLE_TYPES))]
    public async Task EveryExportIsReadAsLuaReadsIt(PluginConfigurationObjectType type)
    {
        var snippet = Export(type);

        Assert.That(ConfigurationSnippetParser.TryParse(snippet, type, out var parsed, out var issue), Is.True, issue);
        AssertSameTable(await ReadWithLuaAsync(type, snippet), parsed, type.ToString());
    }

    [Test]
    public void EveryTypeOfConfigurationObjectHasAnExport() => Assert.That(EXPORTABLE_TYPES, Is.EquivalentTo(PluginConfigurationObjectDescriptors.ALL.Select(descriptor => descriptor.Type)), "A new type of configuration object needs its export in the round trip above.");

    private static IEnumerable<TestCaseData> PairsOfDifferentTypes() =>
        from snippetType in EXPORTABLE_TYPES
        from expectedType in EXPORTABLE_TYPES
        where snippetType != expectedType
        select new TestCaseData(snippetType, expectedType);

    [TestCaseSource(nameof(PairsOfDifferentTypes))]
    public void ASnippetOfAnotherTypeIsRefused(PluginConfigurationObjectType snippetType, PluginConfigurationObjectType expectedType)
    {
        var section = PluginConfigurationObjectDescriptors.Get(snippetType).LuaTableName;
        AssertRefused($$"""CONFIG["{{section}}"][#CONFIG["{{section}}"]+1] = { ["a"] = 1 }""", expectedType);
    }

    [Test]
    // ReSharper disable RedundantArgumentDefaultValue
    public void ASnippetOfAnUnknownSectionIsRefused() => AssertRefused("""CONFIG["SETTINGS"][#CONFIG["SETTINGS"]+1] = { ["a"] = 1 }""", PluginConfigurationObjectType.PROFILE);
    // ReSharper restore RedundantArgumentDefaultValue

    [TestCase("""["a"] = "q\"b\\c\n\r\t\a\b\f\v'" """, TestName = "Escapes in a double-quoted string")]
    [TestCase("""["a"] = 'it\'s "fine"' """, TestName = "Escapes in a single-quoted string")]
    [TestCase("""["a"] = [==[ends with ]] and ]=] but not yet]==]""", TestName = "A long string with a level")]
    [TestCase("[\"a\"] = [[one\r\ntwo\rthree\n\rfour\n\nfive]]", TestName = "Line breaks of every kind in a long string")]
    [TestCase("[\"a\"] = [[\n    indented]]", TestName = "An indented long string starting on its own line")]
    [TestCase("[\"a\"] = [[\r\n\r\nafter an empty line]]", TestName = "A long string starting with two line breaks")]
    [TestCase("""["a"] = "Grüße – 日本", ["Ünïcode"] = true""", TestName = "Text beyond ASCII")]
    [TestCase("""["a"] = 5, ["b"] = -5, ["c"] = 0.25, ["d"] = -.5, ["e"] = 5., ["f"] = 65535""", TestName = "Numbers")]
    [TestCase("""["a"] = true; ["b"] = false, ["c"] = { "x", 'y', [[z]], { ["d"] = 1 }, },""", TestName = "Lists, nested tables, and both separators")]
    [TestCase("""["a"] = {}, ["b"] = { {} }""", TestName = "Empty tables")]
    [TestCase("-- before\n[\"a\"] = -- after the equals sign\n 1, --[ not a block comment\n-- [[ neither\n[\"b\"] = 2 -- last\n", TestName = "Line comments everywhere")]
    public async Task AHandwrittenSnippetIsReadAsLuaReadsIt(string fields) => await AssertReadAsLuaReadsItAsync(Snippet(fields));

    [Test]
    public async Task CommentsBeforeTheAssignmentAreAllowed() => await AssertReadAsLuaReadsItAsync("-- Exported from the laptop of a colleague.\r\n-- Check the IDs below.\r\n\r\n" + Snippet("""["a"] = 1"""));

    [Test]
    public async Task ThirtyNestedTablesAreStillRead() => await AssertReadAsLuaReadsItAsync(Snippet($"""["a"] = {string.Concat(Enumerable.Repeat("{ ", 30))}1{string.Concat(Enumerable.Repeat(" }", 30))}"""));

    [TestCase("""["a"] = print("x")""", TestName = "A function call")]
    [TestCase("""["a"] = function() return 1 end""", TestName = "A function")]
    [TestCase("""["a"] = "x" .. "y" """, TestName = "A concatenation")]
    [TestCase("""["a"] = 1 + 1""", TestName = "Arithmetic")]
    [TestCase("""["a"] = CONFIG""", TestName = "A variable")]
    [TestCase("""["a"] = os.getenv("HOME")""", TestName = "A library call")]
    [TestCase("""["a"] = nil""", TestName = "nil as a field")]
    [TestCase("""["a"] = { "x", nil, "y" }""", TestName = "nil in a list")]
    [TestCase("[\"a\"] = 1, --[[ [\"b\"] = 2, ]] [\"c\"] = 3\n", TestName = "A block comment followed by data on the same line")]
    [TestCase("--[==[\n[\"a\"] = 1,\n--]==]\n[\"b\"] = 2\n", TestName = "A block comment over several lines")]
    [TestCase("""["a"] = 1, ["a"] = 2""", TestName = "A field twice")]
    [TestCase("""a = 1""", TestName = "A name as the key")]
    [TestCase("""[1] = "x" """, TestName = "A number as the key")]
    [TestCase("""["a"] = "\x41" """, TestName = "An escape Lua knows, but exports never write")]
    [TestCase("""["a"] = "unterminated""", TestName = "An unterminated string")]
    [TestCase("""["a"] = [==[unterminated]=]""", TestName = "An unterminated long string")]
    [TestCase("""["a"] = 0x10""", TestName = "A hexadecimal number")]
    public void CodeAndConstructsBeyondPlainDataAreRefused(string fields) => AssertRefused(Snippet(fields));

    [Test]
    public void ASecondStatementIsRefused() => AssertRefused(Snippet("""["a"] = 1""") + "\nos.exit()");

    [Test]
    public void ASecondAssignmentIsRefused() => AssertRefused(Snippet("""["a"] = 1""") + "\n" + Snippet("""["b"] = 2"""));

    [Test]
    public void AnAssignmentWhichCountsAnotherSectionIsRefused() => AssertRefused("""CONFIG["PROFILES"][#CONFIG["CHAT_TEMPLATES"]+1] = { ["a"] = 1 }""");

    [Test]
    public void AnAssignmentToAFixedIndexIsRefused() => AssertRefused("""CONFIG["PROFILES"][1] = { ["a"] = 1 }""");

    [Test]
    public void ATooLargeSnippetIsRefused() => AssertRefused(Snippet($"""["a"] = "{new string('x', 1_000_000)}" """));

    [Test]
    public void TooManyNestedTablesAreRefused() => AssertRefused(Snippet($"""["a"] = {string.Concat(Enumerable.Repeat("{ ", 40))}1{string.Concat(Enumerable.Repeat(" }", 40))}"""));

    [TestCase("")]
    [TestCase("   \r\n  ")]
    public void AnEmptySnippetIsRefused(string snippet) => AssertRefused(snippet);

    /// <summary>
    /// Reads a profile snippet with the parser and with Lua, and compares both.
    /// </summary>
    private static async Task AssertReadAsLuaReadsItAsync(string snippet)
    {
        Assert.That(ConfigurationSnippetParser.TryParse(snippet, PluginConfigurationObjectType.PROFILE, out var parsed, out var issue), Is.True, issue);
        AssertSameTable(await ReadWithLuaAsync(PluginConfigurationObjectType.PROFILE, snippet), parsed, "PROFILES");
    }

    private static void AssertRefused(string snippet, PluginConfigurationObjectType type = PluginConfigurationObjectType.PROFILE)
    {
        var accepted = ConfigurationSnippetParser.TryParse(snippet, type, out _, out var issue);
        Assert.Multiple(() =>
        {
            Assert.That(accepted, Is.False);
            Assert.That(issue, Is.Not.Empty, "A refused snippet needs a reason the user can read.");
        });
    }

    /// <summary>
    /// A profile snippet with these fields.
    /// </summary>
    private static string Snippet(string fields) => $$"""CONFIG["PROFILES"][#CONFIG["PROFILES"]+1] = { {{fields}} }""";

    /// <summary>
    /// What the app exports for one item of each type, with values which the export has to escape.
    /// </summary>
    /// <remarks>
    /// The items are made here rather than in a test case source: NUnit reads such a source before
    /// TestHost runs, and the provider types need its logger factory.
    /// </remarks>
    private static string Export(PluginConfigurationObjectType type) => type switch
    {
        PluginConfigurationObjectType.LLM_PROVIDER => new AIStudio.Settings.Provider(
            1,
            Guid.NewGuid().ToString(),
            QUOTED_TEXT,
            LLMProviders.SELF_HOSTED,
            new Model("llama-3.3-70b", QUOTED_TEXT),
            IsSelfHosted: true,
            Hostname: "http://localhost:11434",
            Host: Host.OLLAMA,
            AdditionalJsonApiParameters: "\"temperature\": 0.5, \"top_p\": 0.9",
            TokenizerPath: WINDOWS_PATH,
            CapabilityOverrides: new ProviderCapabilityOverrides { FunctionCalling = true, ContextWindowTokens = 32_768, MaxImagesPerMessage = 0 })
            .ExportAsConfigurationSection("ENC:v1:c2VjcmV0"),

        PluginConfigurationObjectType.EMBEDDING_PROVIDER => new EmbeddingProvider(
            1,
            Guid.NewGuid().ToString(),
            QUOTED_TEXT,
            LLMProviders.SELF_HOSTED,
            new Model("nomic-embed-text", null),
            IsSelfHosted: true,
            Host: Host.OLLAMA,
            TokenizerPath: WINDOWS_PATH,
            EmbeddingBatchSize: 16,
            TokenLimit: 512)
            .ExportAsConfigurationSection("ENC:v1:c2VjcmV0"),

        PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER => new TranscriptionProvider(
            1,
            Guid.NewGuid().ToString(),
            QUOTED_TEXT,
            LLMProviders.SELF_HOSTED,
            new Model("whisper-large-v3", "Whisper"),
            IsSelfHosted: true,
            Host: Host.WHISPER_CPP)
            .ExportAsConfigurationSection(),

        PluginConfigurationObjectType.PROFILE => new Profile(1, Guid.NewGuid().ToString(), QUOTED_TEXT, MULTILINE_TEXT, "Short actions.").ExportAsConfigurationSection(),

        PluginConfigurationObjectType.CHAT_TEMPLATE => ExportChatTemplate(),

        PluginConfigurationObjectType.DATA_SOURCE => new DataSourceERI_V1
        {
            Num = 1,
            Id = Guid.NewGuid().ToString(),
            Name = QUOTED_TEXT,
            Type = DataSourceType.ERI_V1,
            Hostname = "https://eri.example.org",
            Port = 8443,
            AuthMethod = AuthMethod.USERNAME_PASSWORD,
            Username = "jane.doe",
            SecurityPolicy = DataSourceSecurity.SELF_HOSTED,
            SelectedRetrievalId = "semantic-search",
            MaxMatches = 25,
        }.ExportAsConfigurationSection("ENC:v1:c2VjcmV0", DataSourceERIUsernamePasswordMode.SHARED_USERNAME_AND_PASSWORD),

        PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY => new DataDocumentAnalysisPolicy
        {
            Id = Guid.NewGuid().ToString(),
            PolicyName = QUOTED_TEXT,
            PolicyDescription = "Checks contracts for the clauses our legal department cares about.",
            AnalysisRules = MULTILINE_TEXT,
            OutputRules = "- Answer in a table.\r\n- Quote the clause.",
            MinimumProviderConfidence = ConfidenceLevel.MEDIUM,
            AllowedToolIds = ["web_search", "read_web_page"],
            PreselectedProvider = Guid.NewGuid().ToString(),
            HidePolicyDefinition = true,
        }.ExportAsConfigurationSection(),

        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "This test knows no export for this type."),
    };

    private static string ExportChatTemplate()
    {
        var template = new ChatTemplate
        {
            Num = 1,
            Id = Guid.NewGuid().ToString(),
            Name = QUOTED_TEXT,
            SystemPrompt = MULTILINE_TEXT,
            PredefinedUserPrompt = "Please summarize: ",
            AllowProfileUsage = true,
            FileAttachments = [new FileAttachment(FileAttachmentType.DOCUMENT, "report.pdf", WINDOWS_PATH, 1024)],
            ExampleConversation =
            [
                new ContentBlock { ContentType = ContentType.TEXT, Role = ChatRole.USER, Content = new ContentText { Text = QUOTED_TEXT } },
                new ContentBlock { ContentType = ContentType.TEXT, Role = ChatRole.AI, Content = new ContentText { Text = MULTILINE_TEXT } },
            ],
            ToolIds = ["web_search"],

            // Preselected data sources make the export start with a comment, and so do a preselected
            // provider and profile, with a comment of their own:
            DataSourceOptions = new() { DisableDataSources = false, PreselectedDataSourceIds = [Guid.NewGuid().ToString()] },
            ProviderId = Guid.NewGuid().ToString(),
            ProfileId = Guid.NewGuid().ToString(),
        };

        Assert.That(template.TryExportAsConfigurationSection(out var luaCode, out var issue), Is.True, issue);
        return luaCode;
    }

    /// <summary>
    /// Reads a snippet the way a configuration plugin containing it would be read.
    /// </summary>
    private static async Task<LuaTable> ReadWithLuaAsync(PluginConfigurationObjectType type, string snippet)
    {
        var section = PluginConfigurationObjectDescriptors.Get(type).LuaTableName;
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenTableLibrary();

        await state.DoStringAsync($$"""
                                    CONFIG = {}
                                    CONFIG["{{section}}"] = {}
                                    {{snippet}}
                                    """);

        if (!state.Environment["CONFIG"].TryRead<LuaTable>(out var configTable) ||
            !configTable[section].TryRead<LuaTable>(out var sectionTable) ||
            !sectionTable[1].TryRead<LuaTable>(out var itemTable))
            throw new InvalidOperationException("The snippet of this test did not add a table to its section.");

        return itemTable;
    }

    private static void AssertSameTable(LuaTable expected, LuaTable actual, string path)
    {
        var expectedFields = Fields(expected);
        var actualFields = Fields(actual);
        Assert.That(actualFields.Keys, Is.EquivalentTo(expectedFields.Keys), path);
        foreach (var (key, expectedValue) in expectedFields)
            AssertSameValue(expectedValue, actualFields[key], $"{path}[{key}]");
    }

    private static void AssertSameValue(LuaValue expected, LuaValue actual, string path)
    {
        Assert.That(actual.Type, Is.EqualTo(expected.Type), path);
        switch (expected.Type)
        {
            case LuaValueType.String:
                Assert.That(actual.Read<string>(), Is.EqualTo(expected.Read<string>()), path);
                break;

            case LuaValueType.Number:
                Assert.That(actual.Read<double>(), Is.EqualTo(expected.Read<double>()), path);
                break;

            case LuaValueType.Boolean:
                Assert.That(actual.Read<bool>(), Is.EqualTo(expected.Read<bool>()), path);
                break;

            case LuaValueType.Table:
                AssertSameTable(expected.Read<LuaTable>(), actual.Read<LuaTable>(), path);
                break;

            default:
                Assert.Fail($"{path} holds a {expected.Type}, which no snippet of this test contains.");
                break;
        }
    }

    private static Dictionary<string, LuaValue> Fields(LuaTable table)
    {
        var fields = new Dictionary<string, LuaValue>(StringComparer.Ordinal);
        var key = LuaValue.Nil;
        while (table.TryGetNext(key, out var pair))
        {
            key = pair.Key;
            var name = pair.Key.Type switch
            {
                LuaValueType.String => $"\"{pair.Key.Read<string>()}\"",
                LuaValueType.Number => pair.Key.Read<double>().ToString(CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException($"A key of the type {pair.Key.Type} cannot appear in a snippet."),
            };

            fields.Add(name, pair.Value);
        }

        return fields;
    }
}