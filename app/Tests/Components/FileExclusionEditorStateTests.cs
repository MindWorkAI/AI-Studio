using AIStudio.Components;
using AIStudio.Tools;

namespace AIStudio.Tests.Components;

public sealed class FileExclusionEditorStateTests
{
    [TestCase("")]
    [TestCase(FileExclusions.DEFAULT_PATTERNS)]
    [TestCase(" node_modules/ ; bin/\n*.min.js;draft-* ")]
    public void OpeningTheEditorDoesNotRewriteTheStoredText(string text)
    {
        var state = new FileExclusionEditorState();
        state.Receive(text);

        Assert.That(state.Text, Is.EqualTo(text));
    }

    [TestCase("node_modules/;bin/;*.min.js;draft-*")]
    [TestCase("node_modules/\nbin/\n*.min.js\ndraft-*")]
    [TestCase("node_modules/\r\nbin/\r\n*.min.js\r\ndraft-*")]
    [TestCase("node_modules/; bin/\r\n*.min.js; draft-*")]
    public void BothGroupsDisplaySemicolonSeparatedPatternsWithoutRewritingStoredLists(string text)
    {
        var state = Create(text);

        Assert.Multiple(() =>
        {
            Assert.That(state.FolderText, Is.EqualTo("node_modules/; bin/"));
            Assert.That(state.FileText, Is.EqualTo("*.min.js; draft-*"));
            Assert.That(state.Text, Is.EqualTo(text));
        });
    }

    [Test]
    public void EditingFoldersKeepsEveryFilePattern()
    {
        var state = Create("bin/;*.min.js;draft-*");
        state.EditFolders("obj/\nbuild/");

        Assert.Multiple(() =>
        {
            Assert.That(FileExclusions.Parse(state.Text), Is.EqualTo(new[] { "obj/", "build/", "*.min.js", "draft-*" }));
            Assert.That(state.FileText, Is.EqualTo("*.min.js; draft-*"));
            Assert.That(state.Text, Is.EqualTo("obj/; build/; *.min.js; draft-*"));
        });
    }

    [Test]
    public void EditingFilesKeepsEveryFolderPattern()
    {
        var state = Create("bin/;node_modules/;*.min.js");
        state.EditFiles("*.wav;*.md");

        Assert.Multiple(() =>
        {
            Assert.That(FileExclusions.Parse(state.Text), Is.EqualTo(new[] { "bin/", "node_modules/", "*.wav", "*.md" }));
            Assert.That(state.FolderText, Is.EqualTo("bin/; node_modules/"));
            Assert.That(state.Text, Is.EqualTo("bin/; node_modules/; *.wav; *.md"));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void ClearingOneGroupKeepsTheOtherGroup(string emptyText)
    {
        var state = Create("bin/;*.min.js");
        state.EditFolders(emptyText);
        Assert.That(FileExclusions.Parse(state.Text), Is.EqualTo(new[] { "*.min.js" }));

        state = Create("bin/;*.min.js");
        state.EditFiles(emptyText);
        Assert.That(FileExclusions.Parse(state.Text), Is.EqualTo(new[] { "bin/" }));
    }

    [Test]
    public void ExplicitFolderRulesKeepTheirExistingMatchingRules()
    {
        var state = Create(string.Empty);
        state.EditFolders("node_modules/;cmake-build-*/");
        var patterns = FileExclusions.Parse(state.Text);

        Assert.Multiple(() =>
        {
            Assert.That(patterns, Is.EqualTo(new[] { "node_modules/", "cmake-build-*/" }));
            Assert.That(FileExclusions.IsValid(state.Text), Is.True);
            Assert.That(state.IsValid, Is.True);
            Assert.That(FileExclusions.ExcludesFolder("node_modules", patterns), Is.True);
            Assert.That(FileExclusions.ExcludesFolder("cmake-build-debug", patterns), Is.True);
            Assert.That(FileExclusions.ExcludesFile("node_modules", patterns), Is.False);
        });
    }

    [TestCase(":text:")]
    [TestCase("src/bin/")]
    [TestCase("/")]
    [TestCase("**/*.md")]
    public void EditingTheOtherGroupNeverDropsAnInvalidStoredRule(string invalidRule)
    {
        var state = Create(invalidRule);
        if (invalidRule.EndsWith('/'))
            state.EditFiles("*.tmp");
        else
            state.EditFolders("bin/");

        Assert.Multiple(() =>
        {
            Assert.That(FileExclusions.Parse(state.Text), Does.Contain(invalidRule));
            Assert.That(FileExclusions.IsValid(state.Text), Is.False);
        });
    }

    [TestCase(":text:")]
    [TestCase(":documents:")]
    public void FolderEditingDoesNotTurnAnInvalidSelectorIntoAValidLiteralFolder(string selector)
    {
        var state = Create(string.Empty);
        state.EditFolders(selector);

        Assert.Multiple(() =>
        {
            Assert.That(state.Text, Is.EqualTo(selector));
            Assert.That(FileExclusions.IsValid(state.Text), Is.False);
        });
    }

    [Test]
    public void ExistingFolderNamesWhichResembleSelectorsArePreservedExactly()
    {
        var state = Create(":text:/;*.tmp");
        state.EditFiles("*.bak");

        Assert.That(FileExclusions.Parse(state.Text), Is.EqualTo(new[] { ":text:/", "*.bak" }));
    }

    [Test]
    public void RestoringOneGroupDeduplicatesRulesWithoutLosingOtherRules()
    {
        var state = Create("custom/;*.TMP;*.tmp;Caddyfile");
        state.RestoreFolders("node_modules/;NODE_MODULES/;*.default");

        Assert.That(FileExclusions.Parse(state.Text), Is.EqualTo(new[] { "node_modules/", "*.TMP", "Caddyfile" }));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RestoringOneFieldPreservesTheOtherFieldsDraft(bool folders)
    {
        var state = Create("custom/;*.custom");
        state.EditFolders("latest/; ");
        state.EditFiles("*.latest; ");

        const string DEFAULTS = "bin/;node_modules/;*.tmp;*.bak";
        if (folders)
            state.RestoreFolders(DEFAULTS);
        else
            state.RestoreFiles(DEFAULTS);

        Assert.Multiple(() =>
        {
            Assert.That(state.FolderText, Is.EqualTo(folders ? "bin/; node_modules/" : "latest/; "));
            Assert.That(state.FileText, Is.EqualTo(folders ? "*.latest; " : "*.tmp; *.bak"));
            Assert.That(state.Text, Is.EqualTo(folders ? "bin/; node_modules/; *.latest" : "latest/; *.tmp; *.bak"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void EmptyDefaultsClearOnlyTheRestoredField(bool folders)
    {
        var state = Create("custom/;*.custom");
        if (folders)
            state.RestoreFolders(string.Empty);
        else
            state.RestoreFiles(string.Empty);

        Assert.That(state.Text, Is.EqualTo(folders ? "*.custom" : "custom/"));
    }

    [TestCase(true, ":text:")]
    [TestCase(false, "src/bin/")]
    public void RestoringOneFieldPreservesInvalidRulesInTheOtherField(bool folders, string invalidRule)
    {
        var state = Create(invalidRule);
        if (folders)
            state.RestoreFolders(FileExclusions.DEFAULT_PATTERNS);
        else
            state.RestoreFiles(FileExclusions.DEFAULT_PATTERNS);

        Assert.Multiple(() =>
        {
            Assert.That(FileExclusions.Parse(state.Text), Does.Contain(invalidRule));
            Assert.That(FileExclusions.IsValid(state.Text), Is.False);
        });
    }

    [Test]
    public void EditingTheFileFieldDoesNotSilentlyMoveFolderRules()
    {
        var state = Create("bin/;*.min.js");
        state.EditFiles("obj/;*.wav;draft-*");
        Assert.Multiple(() =>
        {
            Assert.That(state.IsValid, Is.False);
            Assert.That(state.Text, Is.EqualTo("bin/; obj/; *.wav; draft-*"));
            Assert.That(state.FolderText, Is.EqualTo("bin/"));
            Assert.That(state.FileText, Is.EqualTo("obj/;*.wav;draft-*"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void EditingBothVisibleFieldsPreservesBothDraftsAndAllRules(bool foldersFirst)
    {
        var state = Create("bin/;*.tmp");
        string earlierValue;
        if (foldersFirst)
        {
            state.EditFolders("obj/; build/; ");
            earlierValue = state.Text;
            state.EditFiles("*.wav; draft-*; ");
        }
        else
        {
            state.EditFiles("*.wav; draft-*; ");
            earlierValue = state.Text;
            state.EditFolders("obj/; build/; ");
        }

        state.Receive(earlierValue);
        state.Receive(state.Text);

        Assert.Multiple(() =>
        {
            Assert.That(state.FolderText, Is.EqualTo("obj/; build/; "));
            Assert.That(state.FileText, Is.EqualTo("*.wav; draft-*; "));
            Assert.That(state.Text, Is.EqualTo("obj/; build/; *.wav; draft-*"));
        });
    }

    [Test]
    public void ADebouncedEchoDoesNotOverwriteANewerDraft()
    {
        var state = Create("*.original");
        state.EditFiles("*.first");
        var earlierValue = state.Text;
        state.EditFiles("*.latest");
        state.Receive(earlierValue);

        Assert.Multiple(() =>
        {
            Assert.That(state.Text, Is.EqualTo("*.latest"));
            Assert.That(state.FileText, Is.EqualTo("*.latest"));
        });
    }

    [Test]
    public void AnUnchangedParentValueDoesNotDiscardAnUncommittedEdit()
    {
        var state = Create("*.original");
        state.EditFiles("*.latest; ");
        state.Receive("*.original");

        Assert.That(state.FileText, Is.EqualTo("*.latest; "));
    }

    [Test]
    public void ANewExternalValueReplacesBothGroups()
    {
        var state = Create("bin/;*.tmp");
        state.EditFiles("*.latest");
        state.Receive("policy/;*.secret");

        Assert.Multiple(() =>
        {
            Assert.That(state.FolderText, Is.EqualTo("policy/"));
            Assert.That(state.FileText, Is.EqualTo("*.secret"));
        });
    }

    [Test]
    public void ADisabledEditorShowsTheActualSettingEvenWhenItMatchesAnOlderDraft()
    {
        var state = Create("*.original");
        state.EditFiles("*.earlier");
        state.EditFiles("*.latest");
        state.Receive("*.earlier", discardDraft: true);

        Assert.That(state.FileText, Is.EqualTo("*.earlier"));
    }

    [Test]
    public void ReformattingTheDefaultsDoesNotChangeTheSelectionSignature()
    {
        var state = Create(FileExclusions.DEFAULT_PATTERNS);
        state.EditFiles(state.FileText);
        var before = new FolderFileSelection(
            ["*"], FileExclusions.Parse(FileExclusions.DEFAULT_PATTERNS), true, false,
            FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES);
        var after = new FolderFileSelection(
            ["*"], FileExclusions.Parse(state.Text), true, false,
            FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES);

        Assert.That(after.Describe(), Is.EqualTo(before.Describe()));
    }

    [Test]
    public void RestoringDefaultsReplacesBothGroupsAndKeepsTheExactDefaultString()
    {
        var state = Create("custom/;*.custom");
        state.EditFolders(":text:");
        state.Reset(FileExclusions.DEFAULT_PATTERNS);

        Assert.Multiple(() =>
        {
            Assert.That(state.Text, Is.EqualTo(FileExclusions.DEFAULT_PATTERNS));
            Assert.That(FileExclusions.IsValid(state.Text), Is.True);
            Assert.That(state.FolderText, Does.Contain("node_modules/"));
            Assert.That(state.FileText, Does.Contain("*.exe"));
            Assert.That(state.FolderText, Does.Not.Contain("\n").And.Not.Contain("\r"));
            Assert.That(state.FileText, Does.Not.Contain("\n").And.Not.Contain("\r"));
        });
    }

    [Test]
    public void EditingADefaultGroupPreservesAllRulesInTheOtherGroup()
    {
        var state = Create(FileExclusions.DEFAULT_PATTERNS);
        state.EditFiles(state.FileText + "\n*.scratch");

        Assert.That(
            FileExclusions.Parse(state.Text),
            Is.EquivalentTo(FileExclusions.Parse(FileExclusions.DEFAULT_PATTERNS).Append("*.scratch")));
    }

    [TestCase("*.pdf")]
    [TestCase("Dockerfile")]
    [TestCase("node_modules")]
    public void WrongFieldDraftsStayVisibleWithoutAddingAFolderSuffix(string pattern)
    {
        var state = Create("bin/;*.tmp");
        state.EditFolders(pattern);

        Assert.Multiple(() =>
        {
            Assert.That(state.IsValid, Is.False);
            Assert.That(state.FolderText, Is.EqualTo(pattern));
            Assert.That(FileExclusions.Parse(state.Text), Does.Contain(pattern));
            Assert.That(FileExclusions.Parse(state.Text), Does.Not.Contain(pattern + "/"));
            Assert.That(state.FileText, Is.EqualTo("*.tmp"));
        });
    }

    [Test]
    public void FolderRulesInTheFileFieldStayVisibleAndInvalidAfterAParentEcho()
    {
        var state = Create("bin/;*.tmp");
        state.EditFiles("*.bak");
        var earlierValue = state.Text;
        state.EditFiles("*.bak;obj/");
        state.Receive(earlierValue);

        Assert.Multiple(() =>
        {
            Assert.That(state.IsValid, Is.False);
            Assert.That(state.FileText, Is.EqualTo("*.bak;obj/"));
            Assert.That(state.FolderText, Is.EqualTo("bin/"));
        });
    }

    private static FileExclusionEditorState Create(string text)
    {
        var state = new FileExclusionEditorState();
        state.Receive(text);
        return state;
    }
}
