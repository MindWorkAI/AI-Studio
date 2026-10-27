using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.Word;

/// <summary>
/// Numbers the lists of the converted content.
/// </summary>
/// <remarks>
/// The lists get definitions of their own, next to those of the template: one for bullets and one
/// for numbers, each nine levels deep. Every numbered list gets an instance of its own, which
/// starts the count anew; Word would otherwise go on counting where the list before stopped.
/// Bullets have nothing to count, so all bulleted lists share one instance. The part which holds
/// the numbering comes about with the first list, so a document without lists gets none.
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
    /// <returns>The ID of the numbering instance, which the items of the list refer to.</returns>
    public int InstanceFor(bool isOrdered, int start, int level)
    {
        if (!isOrdered)
            return this.bulletInstanceId ??= this.AddInstance(this.bulletDefinitionId ??= this.AddDefinition(false), null);

        var restart = new W.LevelOverride
        {
            LevelIndex = level,
            StartOverrideNumberingValue = new W.StartOverrideNumberingValue { Val = start },
        };

        return this.AddInstance(this.numberDefinitionId ??= this.AddDefinition(true), restart);
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