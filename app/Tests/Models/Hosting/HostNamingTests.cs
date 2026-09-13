using AIStudio.Models;
using AIStudio.Models.Hosting;
using AIStudio.Models.Matching;

namespace AIStudio.Tests.Models.Hosting;

/// <summary>
/// Checks how one wrapping is taken off a name.
/// </summary>
/// <remarks>
/// All of this works on the name as the provider reported it, never on the normalized one, and
/// that is the point worth testing: normalizing writes the slash, the colon, and the spaces all as
/// hyphens, so afterwards there is nothing left to recognize a wrapping by.
/// </remarks>
[TestFixture]
public sealed class HostNamingTests
{
    [Test]
    public void TheOrganizationComesOffAndSaysWhoBuiltTheModel()
    {
        var taken = HostNaming.TrySplitOrganization(new ModelId("anthropic/claude-opus-5"), out var inner, out var vendor);

        Assert.Multiple(() =>
        {
            Assert.That(taken, Is.True);
            Assert.That(inner.Original, Is.EqualTo("claude-opus-5"));
            Assert.That(vendor, Is.EqualTo(ModelVendor.ANTHROPIC));
        });
    }

    [Test]
    public void AnOrganizationNobodyRecognizesStatesNoVendorRatherThanAnUnknownOne()
    {
        //
        // "azure" is where the model is running, not who built it. Saying "unknown" here would be a
        // statement, and it would stop the rules from working out the vendor from the name itself.
        //
        var taken = HostNaming.TrySplitOrganization(new ModelId("azure/gpt-5.6"), out var inner, out var vendor);

        Assert.Multiple(() =>
        {
            Assert.That(taken, Is.True);
            Assert.That(inner.Original, Is.EqualTo("gpt-5.6"));
            Assert.That(vendor, Is.Null);
        });
    }

    [Test]
    public void AnOrganizationIsRecognizedWhicheverWayTheHostSpellsIt()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HostNaming.VendorOfOrganization("meta-llama"), Is.EqualTo(ModelVendor.META));
            Assert.That(HostNaming.VendorOfOrganization("Qwen"), Is.EqualTo(ModelVendor.ALIBABA));
            Assert.That(HostNaming.VendorOfOrganization("deepseek-ai"), Is.EqualTo(ModelVendor.DEEP_SEEK));
            Assert.That(HostNaming.VendorOfOrganization("HuggingFaceTB"), Is.EqualTo(ModelVendor.HUGGING_FACE));
            Assert.That(HostNaming.VendorOfOrganization("somebody-else"), Is.EqualTo(ModelVendor.UNKNOWN));
        });
    }

    [Test]
    public void ANameWithoutAnOrganizationIsLeftAlone()
    {
        var taken = HostNaming.TrySplitOrganization(new ModelId("llama-3.3-70b-versatile"), out var inner, out var vendor);

        Assert.Multiple(() =>
        {
            Assert.That(taken, Is.False);
            Assert.That(inner.Original, Is.EqualTo("llama-3.3-70b-versatile"));
            Assert.That(vendor, Is.Null);
        });
    }

    [Test]
    public void OnlyOneSegmentComesOffAtATime()
    {
        //
        // The account path Fireworks puts in front is three segments deep. Nothing here counts
        // them: the walk asks again, which is also what covers the two wrappings of Hugging Face.
        //
        var taken = HostNaming.TrySplitOrganization(new ModelId("accounts/fireworks/models/llama-v3p1-405b-instruct"), out var inner, out _);

        Assert.Multiple(() =>
        {
            Assert.That(taken, Is.True);
            Assert.That(inner.Original, Is.EqualTo("fireworks/models/llama-v3p1-405b-instruct"));
        });
    }

    [Test]
    public void AnOrganizationWithNothingBehindItIsNotAWrapping()
    {
        var taken = HostNaming.TrySplitOrganization(new ModelId("openai/"), out var inner, out _);

        Assert.Multiple(() =>
        {
            Assert.That(taken, Is.False);
            Assert.That(inner.Original, Is.EqualTo("openai/"));
        });
    }

    [Test]
    public void TheRoutingSuffixComesOffAndTheModelStaysWhatItWas()
    {
        var taken = HostNaming.TryStripRoutingSuffix(new ModelId("google/gemma-4-31B-it:novita"), out var inner);

        Assert.Multiple(() =>
        {
            Assert.That(taken, Is.True);
            Assert.That(inner.Original, Is.EqualTo("google/gemma-4-31B-it"));
        });
    }

    [Test]
    public void AMenuPositionComesOff()
    {
        var taken = HostNaming.TryStripMenuPosition(new ModelId("10 - Muse Glimmer 30b - the newest META model"), out var inner);

        Assert.Multiple(() =>
        {
            Assert.That(taken, Is.True);
            Assert.That(inner.Original, Is.EqualTo("Muse Glimmer 30b - the newest META model"));
        });
    }

    [TestCase("70b-instruct", TestName = "A number the model is named after is not a menu position")]
    [TestCase("3-mini", TestName = "A number followed straight by a hyphen is not a menu position")]
    [TestCase("alias-qwen38-27b", TestName = "A name not starting with a number is not a menu position")]
    [TestCase("Qwen 3.8-27B with DFlash on haicluster", TestName = "A sentence without a leading number is not a menu position")]
    public void WhatOnlyLooksLikeAMenuPositionIsLeftAlone(string modelId)
    {
        var taken = HostNaming.TryStripMenuPosition(new ModelId(modelId), out var inner);

        Assert.Multiple(() =>
        {
            Assert.That(taken, Is.False);
            Assert.That(inner.Original, Is.EqualTo(modelId));
        });
    }
}