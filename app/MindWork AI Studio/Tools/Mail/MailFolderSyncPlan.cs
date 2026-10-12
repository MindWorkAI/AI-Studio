using AIStudio.Tools.Databases.IndexStore;

namespace AIStudio.Tools.Mail;

/// <summary>
/// What a sync pass has to do in one folder, worked out from what the index holds and what the server reports.
/// </summary>
/// <remarks>
/// The server is asked which mails belong into the index -- those of the period, and every flagged
/// one -- and the difference to the index says the rest. A mail which was deleted, moved away, fell
/// out of the period or lost its flag drops out alike; that it may lie elsewhere still is found out
/// by its key, not here.
/// </remarks>
/// <param name="UidValidityChanged">Whether the server voided every UID of the folder. The stored locations go, and every mail counts as new: the ones the index holds are linked again by their key, without being embedded again.</param>
/// <param name="NewUids">The UIDs the index does not hold for this folder, the newest first.</param>
/// <param name="GoneUids">The UIDs the index holds for this folder which no longer belong into it, in ascending order.</param>
/// <param name="KeptUids">The UIDs the index holds and keeps, whose flags may have changed.</param>
/// <param name="ChecksFlags">Whether the flags of the kept mails have to be fetched at all.</param>
/// <param name="FlagsChangedSinceModSeq">Asks only about mails changed since this HIGHESTMODSEQ, or null to ask about every kept mail.</param>
public sealed record MailFolderSyncPlan(bool UidValidityChanged, IReadOnlyList<long> NewUids, IReadOnlyList<long> GoneUids, IReadOnlyList<long> KeptUids, bool ChecksFlags, long? FlagsChangedSinceModSeq)
{
    /// <summary>
    /// Works out the plan for one folder.
    /// </summary>
    /// <param name="storedFolder">The folder as the index stores it, or null when the index does not know it yet.</param>
    /// <param name="storedLocations">The UIDs the index holds for the folder, with their flags.</param>
    /// <param name="serverState">How the folder stands on the server.</param>
    /// <param name="indexedUids">The UIDs which belong into the index, as the server found them.</param>
    /// <returns>The plan.</returns>
    public static MailFolderSyncPlan Create(MailFolderRecord? storedFolder, IReadOnlyDictionary<long, MailFlags> storedLocations, MailFolderState serverState, IReadOnlyCollection<long> indexedUids)
    {
        var uidValidityChanged = storedFolder is not null && storedFolder.UidValidity != serverState.UidValidity;
        HashSet<long> knownUids = uidValidityChanged ? [] : storedLocations.Keys.ToHashSet();
        var wantedUids = indexedUids.ToHashSet();

        var newUids = wantedUids.Where(uid => !knownUids.Contains(uid)).OrderDescending().ToList();
        var goneUids = knownUids.Where(uid => !wantedUids.Contains(uid)).Order().ToList();
        var keptUids = knownUids.Where(wantedUids.Contains).Order().ToList();

        //
        // With CONDSTORE, the HIGHESTMODSEQ of the last complete pass says whether any flag changed
        // since, and if so, the server names the changed mails itself. Without it, or after a pass
        // which did not complete, only fetching every flag tells.
        //
        if (keptUids.Count is 0)
            return new(uidValidityChanged, newUids, goneUids, keptUids, false, null);

        if (storedFolder?.HighestModSeq is { } storedModSeq && serverState.HighestModSeq is { } currentModSeq)
            return new(uidValidityChanged, newUids, goneUids, keptUids, currentModSeq != storedModSeq, storedModSeq);

        return new(uidValidityChanged, newUids, goneUids, keptUids, true, null);
    }

    /// <summary>
    /// Picks the flags which differ from what the index holds.
    /// </summary>
    /// <param name="storedLocations">The UIDs the index holds for the folder, with their flags.</param>
    /// <param name="fetchedFlags">The flags the server reported.</param>
    /// <returns>The changed flags by UID. A UID the index does not hold is left out.</returns>
    public static IReadOnlyDictionary<long, MailFlags> GetChangedFlags(IReadOnlyDictionary<long, MailFlags> storedLocations, IReadOnlyDictionary<long, MailFlags> fetchedFlags) => fetchedFlags
        .Where(entry => storedLocations.TryGetValue(entry.Key, out var storedFlags) && storedFlags != entry.Value)
        .ToDictionary(entry => entry.Key, entry => entry.Value);
}