using AIStudio.Tools.Mail;

using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tools.Databases.IndexStore;

public sealed partial class SqliteIndexStoreClientImplementation
{
    private const string LIKE_ESCAPE = "\\";
    private const string FROM_ROLE = nameof(MailAddressRole.FROM);
    private const string ATTACHMENT_KIND = nameof(MailPartKind.ATTACHMENT);
    private const string NOT_ENCRYPTED = nameof(MailEncryptionKind.NONE);

    private static readonly string[] SENDER_ROLES = [nameof(MailAddressRole.FROM), nameof(MailAddressRole.SENDER)];

    private static readonly string[] RECIPIENT_ROLES = [nameof(MailAddressRole.TO), nameof(MailAddressRole.CC), nameof(MailAddressRole.BCC)];

    public override async Task<IReadOnlyList<string>> QueryMailsAsync(string dataSourceId, MailFilter filter, int offset, int limit, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (limit <= 0)
            return [];

        await using var context = this.CreateContext();
        return await FilterMails(context, dataSourceId, filter)
            .OrderByDescending(mail => mail.ReceivedAtUtc)
            .ThenBy(mail => mail.ParentFileId)
            .Skip(offset)
            .Take(limit)
            .Select(mail => mail.ParentFileId)
            .ToListAsync(token);
    }

    public override async Task<IReadOnlyList<string>> GetMailChunkIdsAsync(string dataSourceId, MailFilter filter, CancellationToken token)
    {
        await using var context = this.CreateContext();
        var mailIds = FilterMails(context, dataSourceId, filter).Select(mail => mail.ParentFileId);
        return await context.EmbeddingChunks
            .AsNoTracking()
            .Where(chunk => mailIds.Contains(chunk.ParentFileId))
            .Select(chunk => chunk.ChunkId)
            .ToListAsync(token);
    }

    public override async Task<IReadOnlyList<IndexStoreSearchResult>> SearchMailChunksAsync(string dataSourceId, string query, MailFilter filter, int maxMatches, CancellationToken token)
    {
        if (maxMatches <= 0)
            return [];

        var ftsQuery = BuildFtsQuery(query);
        if (string.IsNullOrWhiteSpace(ftsQuery))
            return [];

        await using var context = this.CreateContext();
        var mailIds = FilterMails(context, dataSourceId, filter).Select(mail => mail.ParentFileId);
        var results = await InSearchOrder(MatchChunks(context, dataSourceId, ftsQuery).Where(result => mailIds.Contains(result.ParentFileId)))
            .Take(maxMatches)
            .ToListAsync(token);

        return results.Select(ToSearchResult).ToList();
    }

    public override async Task<MailCountResult> CountMailsAsync(string dataSourceId, MailFilter filter, MailCountGrouping grouping, int maxGroups, CancellationToken token)
    {
        await using var context = this.CreateContext();
        var mails = FilterMails(context, dataSourceId, filter);
        var totalCount = await mails.LongCountAsync(token);
        if (totalCount == 0 || maxGroups <= 0 || grouping is MailCountGrouping.NONE)
            return new MailCountResult(totalCount, []);

        //
        // A mail can lie twice in one folder, as two copies under two UIDs, and it can name the same
        // sender in From twice. Either way it is one mail, so the groups count distinct mails:
        //
        var mailIds = mails.Select(mail => mail.ParentFileId);
        var groupQuery = grouping switch
        {
            MailCountGrouping.FOLDER => context.MailLocations
                .Where(location => mailIds.Contains(location.ParentFileId))
                .GroupBy(location => location.Folder!.Path)
                .Select(group => new { group.Key, DisplayName = (string?)string.Empty, Count = group.Select(location => location.ParentFileId).Distinct().LongCount() }),

            MailCountGrouping.SENDER => context.MailAddresses
                .Where(address => address.Role == FROM_ROLE && mailIds.Contains(address.ParentFileId))
                .GroupBy(address => address.Address)
                .Select(group => new { group.Key, DisplayName = group.Max(address => address.DisplayName), Count = group.Select(address => address.ParentFileId).Distinct().LongCount() }),

            _ => throw new ArgumentOutOfRangeException(nameof(grouping), grouping, "There is no such grouping."),
        };

        var groups = await groupQuery
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Key)
            .Take(maxGroups)
            .ToListAsync(token);

        return new MailCountResult(totalCount, groups.Select(group => new MailCountGroup(group.Key, group.DisplayName ?? string.Empty, group.Count)).ToList());
    }

    public override async Task<IReadOnlyList<MailSummary>> GetMailSummariesAsync(string dataSourceId, IReadOnlyList<string> mailIds, CancellationToken token)
    {
        if (mailIds.Count == 0)
            return [];

        var wantedIds = mailIds.Distinct(StringComparer.Ordinal).ToArray();
        await using var context = this.CreateContext();
        var mails = await context.MailMessages
            .AsNoTracking()
            .Where(mail => mail.DataSourceId == dataSourceId && mail.OrphanedAtUtc == null && wantedIds.Contains(mail.ParentFileId))
            .ToDictionaryAsync(mail => mail.ParentFileId, StringComparer.Ordinal, token);

        if (mails.Count == 0)
            return [];

        var foundIds = mails.Keys.ToArray();
        var subjects = await context.EmbeddedFiles
            .AsNoTracking()
            .Where(file => foundIds.Contains(file.ParentFileId))
            .ToDictionaryAsync(file => file.ParentFileId, file => file.FileName, StringComparer.Ordinal, token);

        var addresses = (await context.MailAddresses
                .AsNoTracking()
                .Where(address => foundIds.Contains(address.ParentFileId))
                .ToListAsync(token))
            .ToLookup(address => address.ParentFileId, StringComparer.Ordinal);

        var locations = (await context.MailLocations
                .AsNoTracking()
                .Where(location => foundIds.Contains(location.ParentFileId))
                .Select(location => new { location.ParentFileId, location.Folder!.Path, location.IsSeen, location.IsFlagged, location.IsAnswered })
                .ToListAsync(token))
            .ToLookup(location => location.ParentFileId, StringComparer.Ordinal);

        var attachmentNames = (await context.MailParts
                .AsNoTracking()
                .Where(part => foundIds.Contains(part.ParentFileId) && part.Kind == ATTACHMENT_KIND)
                .OrderBy(part => part.Position)
                .Select(part => new { part.ParentFileId, part.Name })
                .ToListAsync(token))
            .ToLookup(part => part.ParentFileId, part => part.Name, StringComparer.Ordinal);

        return wantedIds
            .Where(mails.ContainsKey)
            .Select(mailId =>
            {
                var mail = mails[mailId];
                var mailLocations = locations[mailId].ToList();
                return new MailSummary(
                    mailId,
                    subjects.GetValueOrDefault(mailId, string.Empty),
                    mail.ReceivedAtUtc,
                    mail.SentAtUtc,
                    mail.MessageId,
                    mail.InReplyTo,
                    ToMailAddressRecords(addresses[mailId]),
                    mailLocations.Select(location => location.Path).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
                    new MailFlags(mailLocations.Any(location => location.IsSeen), mailLocations.Any(location => location.IsFlagged), mailLocations.Any(location => location.IsAnswered)),
                    ParseStoredName(mail.Importance, MailImportance.NORMAL),
                    ParseStoredName(mail.EncryptionKind, MailEncryptionKind.UNKNOWN),
                    attachmentNames[mailId].ToList());
            })
            .ToList();
    }

    public override async Task<string?> FindMailByMessageIdAsync(string dataSourceId, string messageId, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return null;

        await using var context = this.CreateContext();
        return await context.MailMessages
            .AsNoTracking()
            .Where(mail => mail.DataSourceId == dataSourceId && mail.OrphanedAtUtc == null && mail.MessageId == messageId)
            .OrderBy(mail => mail.ReceivedAtUtc)
            .ThenBy(mail => mail.ParentFileId)
            .Select(mail => mail.ParentFileId)
            .FirstOrDefaultAsync(token);
    }

    /// <summary>
    /// The mails of a mailbox which meet the conditions, as a query to build on.
    /// </summary>
    private static IQueryable<MailMessageEntity> FilterMails(IndexStoreDbContext context, string dataSourceId, MailFilter filter)
    {
        var mails = context.MailMessages
            .AsNoTracking()
            .Where(mail => mail.DataSourceId == dataSourceId && mail.OrphanedAtUtc == null);

        if (!string.IsNullOrWhiteSpace(filter.From))
        {
            var pattern = ToContainsPattern(filter.From);
            mails = mails.Where(mail => mail.Addresses.Any(address => SENDER_ROLES.Contains(address.Role) && (EF.Functions.Like(address.Address, pattern, LIKE_ESCAPE) || EF.Functions.Like(address.DisplayName, pattern, LIKE_ESCAPE))));
        }

        if (!string.IsNullOrWhiteSpace(filter.To))
        {
            var pattern = ToContainsPattern(filter.To);
            mails = mails.Where(mail => mail.Addresses.Any(address => RECIPIENT_ROLES.Contains(address.Role) && (EF.Functions.Like(address.Address, pattern, LIKE_ESCAPE) || EF.Functions.Like(address.DisplayName, pattern, LIKE_ESCAPE))));
        }

        if (filter.ReceivedSinceUtc is { } receivedSince)
            mails = mails.Where(mail => mail.ReceivedAtUtc >= receivedSince);

        if (filter.ReceivedBeforeUtc is { } receivedBefore)
            mails = mails.Where(mail => mail.ReceivedAtUtc < receivedBefore);

        if (filter.IsUnread is { } isUnread)
            mails = isUnread
                ? mails.Where(mail => !mail.Locations.Any(location => location.IsSeen))
                : mails.Where(mail => mail.Locations.Any(location => location.IsSeen));

        if (filter.IsFlagged is { } isFlagged)
            mails = isFlagged
                ? mails.Where(mail => mail.Locations.Any(location => location.IsFlagged))
                : mails.Where(mail => !mail.Locations.Any(location => location.IsFlagged));

        if (filter.IsEncrypted is { } isEncrypted)
            mails = isEncrypted
                ? mails.Where(mail => mail.EncryptionKind != NOT_ENCRYPTED)
                : mails.Where(mail => mail.EncryptionKind == NOT_ENCRYPTED);

        if (filter.Importance is { } importance)
        {
            var importanceName = importance.ToString();
            mails = mails.Where(mail => mail.Importance == importanceName);
        }

        if (filter.HasAttachments is { } hasAttachments)
            mails = hasAttachments
                ? mails.Where(mail => mail.Parts.Any(part => part.Kind == ATTACHMENT_KIND))
                : mails.Where(mail => !mail.Parts.Any(part => part.Kind == ATTACHMENT_KIND));

        if (filter.FolderPaths is { } folderPaths)
        {
            var paths = folderPaths.ToArray();
            mails = mails.Where(mail => mail.Locations.Any(location => paths.Contains(location.Folder!.Path)));
        }

        return mails;
    }

    /// <summary>
    /// A LIKE pattern which finds the text anywhere, with any % or _ in it taken literally.
    /// </summary>
    private static string ToContainsPattern(string text)
    {
        var escaped = text.Trim()
            .Replace(LIKE_ESCAPE, LIKE_ESCAPE + LIKE_ESCAPE, StringComparison.Ordinal)
            .Replace("%", LIKE_ESCAPE + "%", StringComparison.Ordinal)
            .Replace("_", LIKE_ESCAPE + "_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}