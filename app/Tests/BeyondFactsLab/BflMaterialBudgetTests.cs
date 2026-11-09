using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Models;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how much of the material of a preparation the BeyondFacts Lab puts into the prompt of a step.
/// </summary>
[TestFixture]
public sealed class BflMaterialBudgetTests
{
    [Test]
    public void TheMaterialGetsHalfOfWhatThePromptLeavesFree()
    {
        var characters = BflMaterialBudget.CharactersFor(ContextWindow.Of(128_000), 90_000);

        Assert.That(characters, Is.EqualTo(((128_000 - BflTokenBudget.RESERVE_TOKENS) * 4 - 90_000) / 2));
    }

    [Test]
    public void EachImageTakesItsRoomBeforeTheText()
    {
        var characters = BflMaterialBudget.CharactersFor(ContextWindow.Of(128_000), 90_000, sentImages: 2);

        Assert.That(characters, Is.EqualTo(((128_000 - BflTokenBudget.RESERVE_TOKENS - 2 * BflMaterialBudget.TOKENS_PER_IMAGE) * 4 - 90_000) / 2));
    }

    [Test]
    public void ImagesBeyondWhatTheModelTakesStayBehind()
    {
        List<BflMaterial> images = [Image("A.png"), Image("B.png"), Image("C.png")];

        var shares = BflMaterialBudget.ShareImages(images, 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shares.Select(share => share.Image), Is.EqualTo(new[] { BflImageDelivery.SENT, BflImageDelivery.SENT, BflImageDelivery.OVER_THE_LIMIT }), "The images the person added first go along.");
            Assert.That(shares.Select(share => share.Material.Name), Is.EqualTo(new[] { "A.png", "B.png", "C.png" }));
            Assert.That(shares.All(share => share.Characters is 0), Is.True, "An image takes no characters.");
        }
    }

    [Test]
    public void AModelWhichSeesNoImagesGetsNone()
    {
        var shares = BflMaterialBudget.ShareImages([Image("A.png"), Image("B.png")], 0);

        Assert.That(shares.Select(share => share.Image), Is.All.EqualTo(BflImageDelivery.MODEL_SEES_NO_IMAGES));
    }

    [Test]
    public void AHugeWindowStillCarriesNoBook()
    {
        Assert.That(BflMaterialBudget.CharactersFor(ContextWindow.Of(1_000_000), 90_000), Is.EqualTo(BflMaterialBudget.MAX_CHARACTERS));
    }

    [Test]
    public void ASmallWindowLeavesNoRoom()
    {
        Assert.That(BflMaterialBudget.CharactersFor(ContextWindow.Of(24_000), 90_000), Is.Zero);
    }

    [Test]
    public void AnUnknownWindowGetsAFixedAmount()
    {
        Assert.That(BflMaterialBudget.CharactersFor(ContextWindow.UNKNOWN, 90_000), Is.EqualTo(BflMaterialBudget.CHARACTERS_WITHOUT_A_KNOWN_CONTEXT_WINDOW));
    }

    [Test]
    public void ShortMaterialComesWholeAndLongMaterialIsShortenedAlike()
    {
        var shares = BflMaterialBudget.Share([Material(10_000), Material(100), Material(50_000)], 20_100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shares.Select(share => share.Characters), Is.EqualTo(new[] { 10_000, 100, 10_000 }), "The order stays the order the person added them in.");
            Assert.That(shares.Select(share => share.IsComplete), Is.EqualTo(new[] { true, true, false }));
            Assert.That(shares[2].Percent, Is.EqualTo(20));
        }
    }

    [Test]
    public void WhatAShortMaterialLeavesGoesToTheOthers()
    {
        var shares = BflMaterialBudget.Share([Material(30_000), Material(1_000), Material(30_000)], 21_000);

        Assert.That(shares.Select(share => share.Characters), Is.EqualTo(new[] { 10_000, 1_000, 10_000 }));
    }

    [Test]
    public void NoRoomMeansNothingOfAnyMaterial()
    {
        var shares = BflMaterialBudget.Share([Material(500), Material(5_000)], 0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shares.Select(share => share.Characters), Is.EqualTo(new[] { 0, 0 }));
            Assert.That(shares.Select(share => share.Percent), Is.EqualTo(new[] { 0, 0 }));
        }
    }

    [Test]
    public void TheSharesNeverExceedTheBudget()
    {
        var materials = Enumerable.Range(1, 17).Select(index => Material(index * 7_919)).ToList();

        var shares = BflMaterialBudget.Share(materials, 123_457);

        Assert.That(shares.Sum(share => share.Characters), Is.LessThanOrEqualTo(123_457));
    }

    [TestCase(1_000, 1, 1)]
    [TestCase(1_000, 999, 99)]
    [TestCase(1_000, 1_000, 100)]
    public void APartIsNeitherNothingNorEverything(int textLength, int characters, int percent)
    {
        Assert.That(new BflMaterialShare(Material(textLength), characters).Percent, Is.EqualTo(percent));
    }

    [Test]
    public void AShortenedMaterialEndsAtALineEnd()
    {
        var beginning = "Erste Zeile\n" + new string('x', 95) + "\nletzt";

        Assert.That(BflMaterialBudget.EndAtLineEnd(beginning), Is.EqualTo("Erste Zeile\n" + new string('x', 95)));
    }

    [Test]
    public void ALineEndFarAwayIsNotWorthTheLoss()
    {
        var beginning = "Kurz\n" + new string('x', 200);

        Assert.That(BflMaterialBudget.EndAtLineEnd(beginning), Is.EqualTo(beginning));
    }

    [Test]
    public void ACutNeverSplitsACharacter()
    {
        var beginning = new string('x', 50) + "😀"[..1];

        Assert.That(BflMaterialBudget.EndAtLineEnd(beginning), Is.EqualTo(new string('x', 50)));
    }

    private static BflMaterial Material(int textLength) => new()
    {
        MaterialId = Guid.NewGuid(),
        Name = $"{textLength}.pdf",
        TextLength = textLength,
    };

    private static BflMaterial Image(string name) => new()
    {
        MaterialId = Guid.NewGuid(),
        Name = name,
        Kind = BflMaterialKind.IMAGE,
        Image = new() { Extension = "png", Width = 100, Height = 100 },
    };
}