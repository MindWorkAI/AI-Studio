using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks who decides the tools and data sources when a direct chat launcher and the chat template
/// it opens its chat with both name some.
/// </summary>
/// <remarks>
/// Either side can be filled in without knowing about the other, so all four combinations happen.
/// The rule is deliberately the same for tools and for data sources: the chat template wins as a
/// whole, because it is the only one of the two which can also leave the choice of sources to an
/// agent, and a field-by-field mix of both would be something neither of them asked for.
/// </remarks>
[TestFixture]
public sealed class ChatTemplatePrecedenceTests
{
    [Test]
    public void WhenNeitherSideSaysAnythingTheChatDefaultsStay()
    {
        var toolChoice = ChatTemplate.ChooseToolIds(NewTemplate(), null);
        var optionsChoice = ChatTemplate.ChooseDataSourceOptions(NewTemplate(), null);

        Assert.Multiple(() =>
        {
            Assert.That(toolChoice.ToolIds, Is.Null, "Nobody named any tool, so the chat has to keep using the tools of its own default.");
            Assert.That(toolChoice.LauncherChoiceDropped, Is.False);
            Assert.That(optionsChoice.Options, Is.Null, "Nobody named any data source, so the chat has to keep using its own default options.");
            Assert.That(optionsChoice.LauncherChoiceDropped, Is.False);
        });
    }

    [Test]
    public void ALauncherAloneDecidesForItself()
    {
        var template = NewTemplate();
        var launcherOptions = NewLauncherOptions("11111111-1111-1111-1111-111111111111");

        var toolChoice = ChatTemplate.ChooseToolIds(template, new[] { "web_search" });
        var optionsChoice = ChatTemplate.ChooseDataSourceOptions(template, launcherOptions);

        Assert.Multiple(() =>
        {
            Assert.That(toolChoice.ToolIds, Is.EquivalentTo(new[] { "web_search" }));
            Assert.That(toolChoice.LauncherChoiceDropped, Is.False, "Nothing was dropped here, so nothing may be reported as dropped either.");
            Assert.That(optionsChoice.Options, Is.SameAs(launcherOptions));
            Assert.That(optionsChoice.LauncherChoiceDropped, Is.False);
        });
    }

    [Test]
    public void ATemplateAloneDecidesForItself()
    {
        var template = NewTemplate() with
        {
            ToolIds = ["read_web_page"],
            DataSourceOptions = new() { DisableDataSources = false, PreselectedDataSourceIds = ["22222222-2222-2222-2222-222222222222"] },
        };

        var toolChoice = ChatTemplate.ChooseToolIds(template, null);
        var optionsChoice = ChatTemplate.ChooseDataSourceOptions(template, null);

        Assert.Multiple(() =>
        {
            Assert.That(toolChoice.ToolIds, Is.EquivalentTo(new[] { "read_web_page" }));
            Assert.That(toolChoice.LauncherChoiceDropped, Is.False);
            Assert.That(optionsChoice.Options!.PreselectedDataSourceIds, Is.EqualTo(new[] { "22222222-2222-2222-2222-222222222222" }));
            Assert.That(optionsChoice.LauncherChoiceDropped, Is.False);
        });
    }

    [Test]
    public void WhenBothSidesSpeakTheTemplateWinsAndTheLossIsReported()
    {
        var template = NewTemplate() with
        {
            ToolIds = ["read_web_page"],
            DataSourceOptions = new() { DisableDataSources = false, PreselectedDataSourceIds = ["22222222-2222-2222-2222-222222222222"] },
        };

        var toolChoice = ChatTemplate.ChooseToolIds(template, new[] { "web_search" });
        var optionsChoice = ChatTemplate.ChooseDataSourceOptions(template, NewLauncherOptions("11111111-1111-1111-1111-111111111111"));

        Assert.Multiple(() =>
        {
            Assert.That(toolChoice.ToolIds, Is.EquivalentTo(new[] { "read_web_page" }));
            Assert.That(toolChoice.LauncherChoiceDropped, Is.True, "The tools of the launcher are gone, and only this flag can make the log say so.");
            Assert.That(optionsChoice.Options!.PreselectedDataSourceIds, Is.EqualTo(new[] { "22222222-2222-2222-2222-222222222222" }));
            Assert.That(optionsChoice.LauncherChoiceDropped, Is.True);
        });
    }

    [Test]
    public void ATemplateWhichWantsNoToolsWinsJustTheSame()
    {
        var template = NewTemplate() with { ToolIds = [] };

        var toolChoice = ChatTemplate.ChooseToolIds(template, new[] { "web_search" });

        Assert.Multiple(() =>
        {
            Assert.That(toolChoice.ToolIds, Is.Empty, "An empty selection is the statement that this template wants no tools, which is as much of a statement as naming one.");
            Assert.That(toolChoice.LauncherChoiceDropped, Is.True);
        });
    }

    [Test]
    public void TheAgenticSelectionOfATemplateSurvivesALauncherWithItsOwnSources()
    {
        var template = NewTemplate() with
        {
            DataSourceOptions = new() { DisableDataSources = false, AutomaticDataSourceSelection = true, PreselectedDataSourceIds = [] },
        };

        var optionsChoice = ChatTemplate.ChooseDataSourceOptions(template, NewLauncherOptions("11111111-1111-1111-1111-111111111111"));

        Assert.Multiple(() =>
        {
            Assert.That(optionsChoice.Options!.AutomaticDataSourceSelection, Is.True, "Letting an agent pick the sources is the one thing a launcher cannot express, so it is exactly what must not be overwritten by one.");
            Assert.That(optionsChoice.Options.PreselectedDataSourceIds, Is.Empty);
            Assert.That(optionsChoice.LauncherChoiceDropped, Is.True);
        });
    }

    [Test]
    public void TheChosenOptionsAreACopyRatherThanTheOnesOfTheTemplate()
    {
        var template = NewTemplate() with
        {
            DataSourceOptions = new() { DisableDataSources = false, PreselectedDataSourceIds = ["22222222-2222-2222-2222-222222222222"] },
        };

        var optionsChoice = ChatTemplate.ChooseDataSourceOptions(template, null);
        optionsChoice.Options!.PreselectedDataSourceIds.Clear();

        Assert.That(template.DataSourceOptions!.PreselectedDataSourceIds, Is.Not.Empty, "The launched chat goes on to change these options, and the template is a setting of the user which must not change with it.");
    }

    /// <summary>
    /// A template which says nothing about tools or data sources.
    /// </summary>
    private static ChatTemplate NewTemplate() => new()
    {
        Num = 1,
        Id = "33333333-3333-3333-3333-333333333333",
        Name = "Intranet Research",
        SystemPrompt = "You are a research assistant.",
    };

    /// <summary>
    /// The options a launcher which names data sources ends up with.
    /// </summary>
    /// <remarks>
    /// A launcher has no switches of its own: it names sources, and the rest is always this. Which
    /// is the whole reason the chat template wins whenever both of them speak.
    /// </remarks>
    private static DataSourceOptions NewLauncherOptions(params string[] dataSourceIds) => new()
    {
        DisableDataSources = false,
        AutomaticDataSourceSelection = false,
        AutomaticValidation = false,
        PreselectedDataSourceIds = [..dataSourceIds],
    };
}