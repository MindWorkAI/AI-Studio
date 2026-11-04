using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.Word;

/// <summary>
/// Numbers the lists of the converted content.
/// </summary>
/// <remarks>
/// A list takes the numbering of the template when the template has one of its kind, e.g., the
/// bullets of the organization; see MarkdownWordConverter.ListLookOf. Otherwise, the lists get
/// definitions of their own, next to those of the template: one for bullets and one for numbers,
/// each nine levels deep. Every numbered list gets an instance of its own, which starts the count
/// anew; Word would otherwise go on counting where the list before stopped. Bullets have nothing to
/// count, so all bulleted lists share one instance. The part which holds the numbering comes about
/// with the first list which needs a numbering of its own, so a document without one gets none.
/// </remarks>
/// <param name="mainPart">The main part of the document.</param>
internal sealed class WordNumbering(MainDocumentPart mainPart)
{
    /// <summary>
    /// The levels of a list Word knows, numbered from 0.
    /// </summary>
    public const int LEVELS = 9;

    /// <summary>
    /// How far each level of a list moves in, in twentieths of a point: half an inch.
    /// </summary>
    private const int INDENTATION_PER_LEVEL = 720;

    /// <summary>
    /// Where the bullet or number stands, left of the text: a quarter inch.
    /// </summary>
    private const int HANGING_INDENTATION = 360;

    private static readonly string[] BULLETS = ["•", "◦", "▪"];

    private W.Numbering? numbering;
    private int? bulletDefinitionId;
    private int? numberDefinitionId;
    private int? bulletInstanceId;

    /// <summary>
    /// Returns where the text of a list item at the given level starts.
    /// </summary>
    /// <param name="level">The level, starting with 0.</param>
    /// <returns>The indentation from the left margin, in twentieths of a point.</returns>
    public static int IndentationOf(int level) => INDENTATION_PER_LEVEL * (level + 1);

    /// <summary>
    /// Returns the numbering of a new list.
    /// </summary>
    /// <param name="isOrdered">Whether the list counts its items rather than marking them with bullets.</param>
    /// <param name="start">The number of the first item of a numbered list.</param>
    /// <param name="level">The level of the list, 0 for a list which no other list holds.</param>
    /// <param name="templateInstance">The numbering instance of the template which the list takes,
    /// see IsOfKind; null for a numbering of its own.</param>
    /// <returns>The ID of the numbering instance, which the items of the list refer to.</returns>
    public int InstanceFor(bool isOrdered, int start, int level, int? templateInstance)
    {
        if (!isOrdered)
            return templateInstance ?? (this.bulletInstanceId ??= this.AddInstance(this.bulletDefinitionId ??= this.AddDefinition(false), null));

        var restart = new W.LevelOverride
        {
            LevelIndex = level,
            StartOverrideNumberingValue = new W.StartOverrideNumberingValue { Val = start },
        };

        // A numbered list of the template counts anew as well, with an instance of the same definition:
        var definitionId = templateInstance is { } instance && this.DefinitionOf(instance) is { } templateDefinition
            ? templateDefinition
            : this.numberDefinitionId ??= this.AddDefinition(true);

        return this.AddInstance(definitionId, restart);
    }

    /// <summary>
    /// Determines whether a numbering instance of the template marks its items the way a list of the given kind does.
    /// </summary>
    /// <param name="instanceId">The ID of the numbering instance.</param>
    /// <param name="isOrdered">Whether the list counts its items rather than marking them with bullets.</param>
    /// <returns>True for bullets and an unordered list, and for numbers or letters and an ordered one.</returns>
    public bool IsOfKind(int instanceId, bool isOrdered)
    {
        var format = this.LevelOf(instanceId, 0)?.NumberingFormat?.Val?.InnerText;
        return format is not null and not "none" && (format is "bullet") != isOrdered;
    }

    /// <summary>
    /// Returns where the text of a list item at the given level starts, as its numbering states it.
    /// </summary>
    /// <remarks>
    /// The paragraphs of an item after the first one carry no bullet or number, but start where
    /// the text of the first one does.
    /// </remarks>
    /// <param name="instanceId">The ID of the numbering instance of the list.</param>
    /// <param name="level">The level of the list, starting with 0.</param>
    /// <returns>The indentation from the left margin, in twentieths of a point.</returns>
    public int TextIndentationOf(int instanceId, int level) => LeftOf(this.LevelOf(instanceId, level)?.PreviousParagraphProperties?.Indentation) ?? IndentationOf(level);

    /// <summary>
    /// Returns where the text of a paragraph with the given indentation starts, apart from its first line.
    /// </summary>
    /// <param name="indentation">The indentation, e.g., of a level of a numbering.</param>
    /// <returns>The indentation from the left margin, in twentieths of a point; null when it states none.</returns>
    public static int? LeftOf(W.Indentation? indentation) => int.TryParse(indentation?.Left?.Value ?? indentation?.Start?.Value, out var left) ? left : null;

    /// <summary>
    /// The numbering of the document as it is, without adding a part for it.
    /// </summary>
    private W.Numbering? ExistingNumbering => this.numbering ?? mainPart.NumberingDefinitionsPart?.Numbering;

    private int? DefinitionOf(int instanceId)
    {
        var instance = this.ExistingNumbering?.Elements<W.NumberingInstance>().FirstOrDefault(candidate => candidate.NumberID?.Value == instanceId);
        return instance?.AbstractNumId?.Val?.Value;
    }

    private W.Level? LevelOf(int instanceId, int level)
    {
        if (this.DefinitionOf(instanceId) is not { } definitionId)
            return null;

        var definition = this.ExistingNumbering?.Elements<W.AbstractNum>().FirstOrDefault(candidate => candidate.AbstractNumberId?.Value == definitionId);
        return definition?.Elements<W.Level>().FirstOrDefault(candidate => candidate.LevelIndex?.Value == level);
    }

    private W.Numbering Numbering
    {
        get
        {
            if (this.numbering is not null)
                return this.numbering;

            var numberingPart = mainPart.NumberingDefinitionsPart ?? mainPart.AddNewPart<NumberingDefinitionsPart>();
            return this.numbering = numberingPart.Numbering ??= new W.Numbering();
        }
    }

    private int AddDefinition(bool isOrdered)
    {
        var id = this.Numbering.Elements<W.AbstractNum>().Select(existing => existing.AbstractNumberId?.Value ?? 0).DefaultIfEmpty(-1).Max() + 1;
        var definition = new W.AbstractNum(new W.MultiLevelType { Val = W.MultiLevelValues.HybridMultilevel }) { AbstractNumberId = id };
        for (var level = 0; level < LEVELS; level++)
            definition.Append(new W.Level
            {
                LevelIndex = level,
                StartNumberingValue = new W.StartNumberingValue { Val = 1 },
                NumberingFormat = new W.NumberingFormat { Val = isOrdered ? W.NumberFormatValues.Decimal : W.NumberFormatValues.Bullet },
                LevelText = new W.LevelText { Val = isOrdered ? $"%{level + 1}." : BULLETS[level % BULLETS.Length] },
                LevelJustification = new W.LevelJustification { Val = W.LevelJustificationValues.Left },
                PreviousParagraphProperties = new W.PreviousParagraphProperties
                {
                    Indentation = new W.Indentation { Left = IndentationOf(level).ToString(), Hanging = HANGING_INDENTATION.ToString() },
                },
            });

        // The file lists all definitions before the instances which refer to them:
        this.Insert(definition, (OpenXmlElement?)this.Numbering.GetFirstChild<W.NumberingInstance>() ?? this.Numbering.GetFirstChild<W.NumberingIdMacAtCleanup>());
        return id;
    }

    private int AddInstance(int definitionId, W.LevelOverride? restart)
    {
        var id = this.Numbering.Elements<W.NumberingInstance>().Select(existing => existing.NumberID?.Value ?? 0).DefaultIfEmpty(0).Max() + 1;
        var instance = new W.NumberingInstance(new W.AbstractNumId { Val = definitionId }) { NumberID = id };
        if (restart is not null)
            instance.Append(restart);

        this.Insert(instance, this.Numbering.GetFirstChild<W.NumberingIdMacAtCleanup>());
        return id;
    }

    /// <summary>
    /// Inserts an element into the numbering, before the first element which has to follow it.
    /// </summary>
    /// <param name="element">The element to insert.</param>
    /// <param name="follower">The first element which has to follow it; null to append it.</param>
    private void Insert(OpenXmlElement element, OpenXmlElement? follower)
    {
        if (follower is null)
            this.Numbering.Append(element);
        else
            this.Numbering.InsertBefore(element, follower);
    }
}