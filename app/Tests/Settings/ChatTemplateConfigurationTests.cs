using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Lua;
using Lua.Standard;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks the tools and data sources a chat template carries, across the two surfaces which have
/// to agree on them: the Lua a configuration plugin states, and the Lua the app exports.
/// </summary>
/// <remarks>
/// The interesting part is not that a value survives, but that the difference between "this
/// template says nothing" and "this template says none" survives. Both end up as an empty
/// selection in the chat on a fresh installation, so a mistake here stays invisible until somebody
/// sets a default tool for their chats -- and then quietly hands out a tool the template ruled out.
/// The last tests cover what the export says out loud before it runs, for the same reason: a data
/// source which cannot be rolled out goes unnoticed on the machine reading the plugin.
/// </remarks>
[TestFixture]
public sealed class ChatTemplateConfigurationTests
{
    private static readonly Guid PLUGIN_ID = new("22222222-2222-2222-2222-222222222222");

    [Test]
    public async Task WhatTheAppExportsIsWhatAConfigurationPluginCanReadBack()
    {
        var written = NewTemplate() with
        {
            ToolIds = ["read_web_page", "web_search"],
            DataSourceOptions = new()
            {
                DisableDataSources = false,
                AutomaticDataSourceSelection = false,
                AutomaticValidation = true,
                PreselectedDataSourceIds = ["11111111-1111-1111-1111-111111111111"],
            },
        };

        var read = await ExportAndReadBackAsync(written);

        Assert.Multiple(() =>
        {
            Assert.That(read.ToolIds, Is.EquivalentTo(written.ToolIds!));
            Assert.That(read.DataSourceOptions, Is.Not.Null);
            Assert.That(read.DataSourceOptions!.DisableDataSources, Is.False);
            Assert.That(read.DataSourceOptions.AutomaticDataSourceSelection, Is.False);
            Assert.That(read.DataSourceOptions.AutomaticValidation, Is.True);
            Assert.That(read.DataSourceOptions.PreselectedDataSourceIds, Is.EqualTo(written.DataSourceOptions!.PreselectedDataSourceIds));
        });
    }

    [Test]
    public async Task AnAgenticSelectionSurvivesTheExportAsWell()
    {
        var written = NewTemplate() with
        {
            DataSourceOptions = new()
            {
                DisableDataSources = false,
                AutomaticDataSourceSelection = true,
                AutomaticValidation = true,
                PreselectedDataSourceIds = [],
            },
        };

        var read = await ExportAndReadBackAsync(written);

        Assert.Multiple(() =>
        {
            Assert.That(read.DataSourceOptions, Is.Not.Null);
            Assert.That(read.DataSourceOptions!.AutomaticDataSourceSelection, Is.True, "Letting an agent pick the sources is the one thing only a chat template can state, so it must not be lost on the way through Lua.");
            Assert.That(read.DataSourceOptions.PreselectedDataSourceIds, Is.Empty);
            Assert.That(read.ToolIds, Is.Null);
        });
    }

    [Test]
    public async Task ATemplateWhichSaysNothingExportsNeitherTable()
    {
        var written = NewTemplate();
        Assert.That(written.TryExportAsConfigurationSection(out var luaCode, out var issue), Is.True, issue);

        Assert.Multiple(() =>
        {
            Assert.That(luaCode, Does.Not.Contain("ToolIds"));
            Assert.That(luaCode, Does.Not.Contain("DataSourceOptions"));
        });

        var read = await ParseAsync(luaCode);

        Assert.Multiple(() =>
        {
            Assert.That(read.ToolIds, Is.Null, "A template without a tool selection must stay without one, so the chat keeps using its own default.");
            Assert.That(read.DataSourceOptions, Is.Null);
        });
    }

    [Test]
    public async Task ATemplateWhichRulesOutEveryToolStaysThatWay()
    {
        var written = NewTemplate() with { ToolIds = [] };
        var read = await ExportAndReadBackAsync(written);

        Assert.That(read.ToolIds, Is.Not.Null, "An empty selection is the statement that this template wants no tools. Reading it back as null would hand out the chat default instead.");
        Assert.That(read.ToolIds, Is.Empty);
    }

    [Test]
    public async Task NamingTheDataSourceOptionsAtAllSwitchesDataSourcesOn()
    {
        var read = await ParseAsync("""
                                    CONFIG["CHAT_TEMPLATES"][#CONFIG["CHAT_TEMPLATES"]+1] = {
                                        ["Id"] = "33333333-3333-3333-3333-333333333333",
                                        ["Name"] = "Intranet Research",
                                        ["SystemPrompt"] = "You are a research assistant.",
                                        ["DataSourceOptions"] = {
                                            ["PreselectedDataSourceIds"] = {
                                                "11111111-1111-1111-1111-111111111111",
                                            },
                                        },
                                    }
                                    """);

        Assert.That(read.DataSourceOptions, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(read.DataSourceOptions!.DisableDataSources, Is.False, "Writing this table is already the statement that the template wants data sources, so an omitted switch must not turn them off again.");
            Assert.That(read.DataSourceOptions.AutomaticDataSourceSelection, Is.False);
            Assert.That(read.DataSourceOptions.AutomaticValidation, Is.False);
            Assert.That(read.DataSourceOptions.PreselectedDataSourceIds, Is.EqualTo(new[] { "11111111-1111-1111-1111-111111111111" }));
        });
    }

    [Test]
    public async Task AnUnusableEntryIsSkippedAndTheRestOfTheListSurvives()
    {
        var read = await ParseAsync("""
                                    CONFIG["CHAT_TEMPLATES"][#CONFIG["CHAT_TEMPLATES"]+1] = {
                                        ["Id"] = "33333333-3333-3333-3333-333333333333",
                                        ["Name"] = "Intranet Research",
                                        ["SystemPrompt"] = "You are a research assistant.",
                                        ["ToolIds"] = {
                                            "web_search",
                                            "",
                                            {},
                                            "read_web_page",
                                        },
                                        ["DataSourceOptions"] = {
                                            ["PreselectedDataSourceIds"] = {
                                                "11111111-1111-1111-1111-111111111111",
                                                "   ",
                                            },
                                        },
                                    }
                                    """);

        Assert.Multiple(() =>
        {
            Assert.That(read.ToolIds, Is.EquivalentTo(new[] { "web_search", "read_web_page" }));
            Assert.That(read.DataSourceOptions!.PreselectedDataSourceIds, Is.EqualTo(new[] { "11111111-1111-1111-1111-111111111111" }));
        });
    }

    [Test]
    public void ExportedDataSourceIdsComeWithTheNoteThatTheyAreLocalOnes()
    {
        var withSources = NewTemplate() with
        {
            DataSourceOptions = new() { DisableDataSources = false, PreselectedDataSourceIds = ["11111111-1111-1111-1111-111111111111"] },
        };

        var agenticOnly = NewTemplate() with
        {
            DataSourceOptions = new() { DisableDataSources = false, AutomaticDataSourceSelection = true },
        };

        Assert.That(withSources.TryExportAsConfigurationSection(out var withSourcesLua, out var issue), Is.True, issue);
        Assert.That(agenticOnly.TryExportAsConfigurationSection(out var agenticOnlyLua, out issue), Is.True, issue);

        Assert.Multiple(() =>
        {
            Assert.That(withSourcesLua, Does.StartWith("--"), "Whoever pastes this into a plugin cannot see from the IDs alone that they belong to another machine.");
            Assert.That(agenticOnlyLua, Does.Not.StartWith("--"), "Without IDs there is nothing to check, so the note would only be noise.");
        });
    }

    [Test]
    public void LocalDataSourcesOfATemplateAreNamedBeforeItIsExported()
    {
        var template = NewTemplate() with
        {
            DataSourceOptions = new()
            {
                DisableDataSources = false,
                PreselectedDataSourceIds = ["11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222"],
            },
        };

        var localNames = ChatTemplate.GetPreselectedLocalDataSourceNames(template, ConfiguredDataSources());

        Assert.That(localNames, Is.EqualTo(new[] { "Meeting notes" }), "Only the local source can be named: its ID means nothing on the machine which reads the exported plugin, while the ERI source points at something the whole organization reaches.");
    }

    [Test]
    public void ATemplateWithoutLocalDataSourcesIsExportedWithoutAQuestion()
    {
        var eriOnly = NewTemplate() with
        {
            DataSourceOptions = new() { DisableDataSources = false, PreselectedDataSourceIds = ["22222222-2222-2222-2222-222222222222"] },
        };

        var agentic = NewTemplate() with
        {
            DataSourceOptions = new() { DisableDataSources = false, AutomaticDataSourceSelection = true },
        };

        Assert.Multiple(() =>
        {
            Assert.That(ChatTemplate.GetPreselectedLocalDataSourceNames(eriOnly, ConfiguredDataSources()), Is.Empty);
            Assert.That(ChatTemplate.GetPreselectedLocalDataSourceNames(agentic, ConfiguredDataSources()), Is.Empty, "An agent picks the sources per message, so this template names none to begin with.");
            Assert.That(ChatTemplate.GetPreselectedLocalDataSourceNames(NewTemplate(), ConfiguredDataSources()), Is.Empty);
        });
    }

    [Test]
    public void AnIdWhichMatchesNoDataSourceIsNotReportedAsALocalOne()
    {
        var template = NewTemplate() with
        {
            DataSourceOptions = new() { DisableDataSources = false, PreselectedDataSourceIds = ["99999999-9999-9999-9999-999999999999"] },
        };

        Assert.That(ChatTemplate.GetPreselectedLocalDataSourceNames(template, ConfiguredDataSources()), Is.Empty, "There is no name to warn about, and the note above the exported IDs already tells the admin to check them.");
    }

    /// <summary>
    /// A template with the parts every export needs, and nothing said about tools or data sources.
    /// </summary>
    private static ChatTemplate NewTemplate() => new()
    {
        Num = 1,
        Id = "33333333-3333-3333-3333-333333333333",
        Name = "Intranet Research",
        SystemPrompt = "You are a research assistant.",
        PredefinedUserPrompt = string.Empty,
        ExampleConversation = [],
        FileAttachments = [],
        AllowProfileUsage = true,
    };

    /// <summary>
    /// One data source of each kind: a local one, which cannot be rolled out, and an ERI one, which can.
    /// </summary>
    private static IReadOnlyList<IDataSource> ConfiguredDataSources() =>
    [
        new DataSourceLocalFile { Id = "11111111-1111-1111-1111-111111111111", Name = "Meeting notes", Type = DataSourceType.LOCAL_FILE },
        new DataSourceERI_V1 { Id = "22222222-2222-2222-2222-222222222222", Name = "Intranet", Type = DataSourceType.ERI_V1 },
    ];

    private static async Task<ChatTemplate> ExportAndReadBackAsync(ChatTemplate template)
    {
        Assert.That(template.TryExportAsConfigurationSection(out var luaCode, out var issue), Is.True, issue);
        return await ParseAsync(luaCode);
    }

    /// <summary>
    /// Reads a chat template the way a configuration plugin states it.
    /// </summary>
    /// <remarks>
    /// Through a real Lua state rather than a table put together in C#, so that the exported code
    /// has to be valid Lua before anything else is checked.
    /// </remarks>
    /// <param name="luaCode">The lines a plugin would contain, including the assignment itself.</param>
    /// <returns>The chat template read from it.</returns>
    private static async Task<ChatTemplate> ParseAsync(string luaCode)
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenTableLibrary();

        await state.DoStringAsync($$"""
                                    CONFIG = {}
                                    CONFIG["CHAT_TEMPLATES"] = {}
                                    {{luaCode}}
                                    """);

        if (!state.Environment["CONFIG"].TryRead<LuaTable>(out var configTable) ||
            !configTable["CHAT_TEMPLATES"].TryRead<LuaTable>(out var templatesTable) ||
            !templatesTable[1].TryRead<LuaTable>(out var templateTable))
            throw new InvalidOperationException("The code of this test did not produce a chat template table.");

        if (!ChatTemplate.TryParseChatTemplateTable(1, templateTable, PLUGIN_ID, string.Empty, out var parsed) || parsed is not ChatTemplate chatTemplate)
            throw new InvalidOperationException("The chat template of this test could not be read.");

        return chatTemplate;
    }
}