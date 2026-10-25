using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.RAG.RAGProcesses;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how the classic RAG process tells the model about ERI data sources whose server could not be reached.
/// </summary>
/// <remarks>
/// Such a data source is never searched. Without a word about it, the model would take its silence
/// for finding nothing and answer from its own knowledge. Semantic Search names it in the
/// description of its tool, the classic process next to the passages it found, and both name the
/// same data sources of the chat. When Semantic Search has nothing to search, its tool stays out,
/// and the classic process writes the note into the system prompt while it steps back.
/// </remarks>
[TestFixture]
public sealed class ClassicRagUnreachableTests
{
    private static readonly IDataSource HANDBOOK = new DataSourceLocalDirectory { Num = 1, Id = "11111111-1111-1111-1111-111111111111", Name = "Handbook", MaxMatches = 10 };
    private static readonly IDataSource INTRANET = new DataSourceERI_V1 { Num = 3, Id = "33333333-3333-3333-3333-333333333333", Name = "Intranet", MaxMatches = 10 };
    private static readonly IDataSource WIKI = new DataSourceERI_V1 { Num = 4, Id = "44444444-4444-4444-4444-444444444444", Name = "Wiki", MaxMatches = 10 };

    [Test]
    public void TheNoteFollowsThePassages()
    {
        var augmentedData = AISrcSelWithRetCtxVal.WithUnreachableDataSources("# Retrieval context 1 of 1\n", [INTRANET]);

        Assert.Multiple(() =>
        {
            Assert.That(augmentedData, Does.StartWith("# Retrieval context 1 of 1"), "What was found stays first.");
            Assert.That(augmentedData, Does.Contain("could not be reached or signed in to right now"));
            Assert.That(augmentedData, Does.Contain("- name='Intranet'"), "The model has to know what is missing, so that it can tell the user.");
        });
    }

    [Test]
    public void WithoutPassagesTheNoteStandsAlone()
    {
        var augmentedData = AISrcSelWithRetCtxVal.WithUnreachableDataSources(string.Empty, [INTRANET]);

        Assert.That(augmentedData, Does.StartWith("These external data sources of this chat could not be reached"), "Every selected data source may be out of reach, and then nothing was retrieved at all.");
    }

    [Test]
    public void WhenEveryServerAnsweredNothingIsAdded()
    {
        const string PASSAGES = "# Retrieval context 1 of 1\n";

        Assert.Multiple(() =>
        {
            Assert.That(AISrcSelWithRetCtxVal.WithUnreachableDataSources(PASSAGES, []), Is.EqualTo(PASSAGES));
            Assert.That(AISrcSelWithRetCtxVal.WithUnreachableDataSources(string.Empty, []), Is.Empty, "The system prompt stays as it was without data sources.");
        });
    }

    [Test]
    public void TheAutomaticSelectionNamesEveryUnreachableDataSource()
    {
        var unreachable = Checked([INTRANET, WIKI]).GetUnreachableOfTheChat(new DataSourceOptions { AutomaticDataSourceSelection = true });

        Assert.That(unreachable, Is.EqualTo(new[] { INTRANET, WIKI }), "The selection agent might have picked any of them.");
    }

    [Test]
    public void TheUserSelectionNamesOnlyTheSelectedUnreachableDataSources()
    {
        var options = new DataSourceOptions { AutomaticDataSourceSelection = false, PreselectedDataSourceIds = [INTRANET.Id] };

        Assert.That(Checked([INTRANET, WIKI]).GetUnreachableOfTheChat(options), Is.EqualTo(new[] { INTRANET }), "The model must not learn about a data source the user did not select for this chat.");
    }

    [Test]
    public void AnUnreachableDataSourceWithoutMatchesIsNotNamed()
    {
        var silent = new DataSourceERI_V1 { Num = 5, Id = "55555555-5555-5555-5555-555555555555", Name = "Archive", MaxMatches = 0 };

        Assert.That(Checked([silent]).GetUnreachableOfTheChat(new DataSourceOptions { AutomaticDataSourceSelection = true }), Is.Empty, "It would add nothing even when its server answers.");
    }

    [Test]
    public void WithoutSearchableDataSourcesTheSystemPromptNamesTheUnreachableOnes()
    {
        var note = AISrcSelWithRetCtxVal.NoteWithoutSearchableDataSources(new OfferedDataSources([], [INTRANET]));

        Assert.That(note, Does.Contain("- name='Intranet'"), "Semantic Search stays out of the request, so nothing else tells the model what is missing.");
    }

    [Test]
    public void WhileTheModelCanSearchTheSystemPromptStaysAsItWas()
    {
        var note = AISrcSelWithRetCtxVal.NoteWithoutSearchableDataSources(new OfferedDataSources([HANDBOOK], [INTRANET]));

        Assert.That(note, Is.Empty, "The description of the tool names the unreachable data sources already.");
    }

    [Test]
    public void WithoutAnyDataSourceTheSystemPromptStaysAsItWas()
    {
        Assert.That(AISrcSelWithRetCtxVal.NoteWithoutSearchableDataSources(OfferedDataSources.NONE), Is.Empty);
    }

    private static AllowedSelectedDataSources Checked(IReadOnlyList<IDataSource> unreachable) => new([], [], [], [], unreachable);
}