using AIStudio.Chat.Compression;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Chat.Compression;

/// <summary>
/// Checks what a chat does with a message which would fill the context window, as the settings say.
/// </summary>
/// <remarks>
/// A wrong answer here either sends a message which the provider refuses without asking, compresses a chat which the
/// user wanted to keep, or compresses again and again.
/// </remarks>
[TestFixture]
public sealed class ContextWindowFullHandlingTests
{
    [TestCase(ContextWindowFullBehavior.ASK, ChatCompressionOffer.OFFERED, true, true, ContextWindowFullAction.ASK, TestName = "Asking asks, also when compressing would help")]
    [TestCase(ContextWindowFullBehavior.SEND_WITHOUT_ASKING, ChatCompressionOffer.OFFERED, true, true, ContextWindowFullAction.SEND, TestName = "Sending without asking sends")]
    [TestCase(ContextWindowFullBehavior.SEND_WITHOUT_ASKING, ChatCompressionOffer.NONE, false, true, ContextWindowFullAction.ASK, TestName = "A place which sends no message that no longer fits asks")]
    [TestCase(ContextWindowFullBehavior.COMPRESS_INTO_NEW_CHAT, ChatCompressionOffer.OFFERED, true, true, ContextWindowFullAction.COMPRESS, TestName = "Compressing into a new chat compresses")]
    [TestCase(ContextWindowFullBehavior.COMPRESS_AND_REPLACE, ChatCompressionOffer.OFFERED, true, true, ContextWindowFullAction.COMPRESS, TestName = "Compressing in place compresses")]
    [TestCase(ContextWindowFullBehavior.COMPRESS_AND_REPLACE, ChatCompressionOffer.OFFERED, false, true, ContextWindowFullAction.COMPRESS, TestName = "Compressing does not need sending anyway")]
    [TestCase(ContextWindowFullBehavior.COMPRESS_AND_REPLACE, ChatCompressionOffer.KEEPS_TOO_MUCH, true, true, ContextWindowFullAction.ASK, TestName = "A compressing which does not help asks")]
    [TestCase(ContextWindowFullBehavior.COMPRESS_INTO_NEW_CHAT, ChatCompressionOffer.NONE, true, true, ContextWindowFullAction.ASK, TestName = "An assistant, which compresses nothing, asks")]
    [TestCase(ContextWindowFullBehavior.COMPRESS_INTO_NEW_CHAT, ChatCompressionOffer.OFFERED, true, false, ContextWindowFullAction.ASK, TestName = "A chat compressed for this message already asks")]
    public void WhatHappensWithAMessageWhichFillsTheWindow(ContextWindowFullBehavior behavior, ChatCompressionOffer offer, bool canSendAnyway, bool mayCompressAutomatically, ContextWindowFullAction expected)
    {
        Assert.That(ContextWindowFullHandling.Decide(behavior, offer, canSendAnyway, mayCompressAutomatically), Is.EqualTo(expected));
    }

    [TestCase(ContextWindowFullBehavior.ASK, false, ChatCompressionTarget.NEW_CHAT, TestName = "Compressing after asking continues in a new chat")]
    [TestCase(ContextWindowFullBehavior.COMPRESS_INTO_NEW_CHAT, false, ChatCompressionTarget.NEW_CHAT, TestName = "Compressing into a new chat continues there")]
    [TestCase(ContextWindowFullBehavior.COMPRESS_AND_REPLACE, false, ChatCompressionTarget.SAME_CHAT, TestName = "Compressing in place replaces the earlier messages")]
    [TestCase(ContextWindowFullBehavior.ASK, true, ChatCompressionTarget.SAME_CHAT, TestName = "Without workspaces, asking compresses in place")]
    [TestCase(ContextWindowFullBehavior.COMPRESS_INTO_NEW_CHAT, true, ChatCompressionTarget.SAME_CHAT, TestName = "Without workspaces, there is no new chat to continue in")]
    public void WhereTheConversationGoesOn(ContextWindowFullBehavior behavior, bool workspacesDisabled, ChatCompressionTarget expected)
    {
        Assert.That(ContextWindowFullHandling.TargetFor(behavior, workspacesDisabled), Is.EqualTo(expected));
    }
}