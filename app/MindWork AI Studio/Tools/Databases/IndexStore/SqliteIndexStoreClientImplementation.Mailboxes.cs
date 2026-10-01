using AIStudio.Tools.Mail;

using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tools.Databases.IndexStore;

public sealed partial class SqliteIndexStoreClientImplementation
{
    public override async Task<IReadOnlyList<MailFolderRecord>> GetMailFoldersAsync(string dataSourceId, CancellationToken token)
    {
        await using var context = this.CreateContext();
        var folders = await context.MailFolders
            .AsNoTracking()
            .Where(folder => folder.DataSourceId == dataSourceId)
            .OrderBy(folder => folder.Path)
            .ToListAsync(token);

        return folders.Select(ToMailFolderRecord).ToList();
    }

    public override async Task UpsertMailFolderAsync(string dataSourceId, MailFolderRecord folder, CancellationToken token)
    {
        await using var context = this.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(token);

        var folderEntity = await context.MailFolders.FirstOrDefaultAsync(entity => entity.DataSourceId == dataSourceId && entity.Path == folder.Path, token);
        if (folderEntity is null)
        {
            folderEntity = new MailFolderEntity
            {
                DataSourceId = dataSourceId,
                Path = folder.Path,
            };
            context.MailFolders.Add(folderEntity);
        }
        else if (folderEntity.UidValidity != folder.UidValidity)
        {
            var folderId = folderEntity.Id;
            await DropMailLocationsAsync(context, context.MailLocations.Where(location => location.FolderId == folderId), token);
        }

        ApplyMailFolder(folderEntity, folder);
        await context.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    public override async Task DeleteMailFolderAsync(string dataSourceId, string folderPath, CancellationToken token)
    {
        await using var context = this.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(token);

        if (await FindMailFolderIdAsync(context, dataSourceId, folderPath, token) is not { } folderId)
            return;

        await DropMailLocationsAsync(context, context.MailLocations.Where(location => location.FolderId == folderId), token);
        await context.MailFolders.Where(folder => folder.Id == folderId).ExecuteDeleteAsync(token);
        await transaction.CommitAsync(token);
    }

    public override async Task UpsertMailAsync(string dataSourceId, MailRecord mail, CancellationToken token)
    {
        ValidateMail(mail);

        await using var context = this.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(token);

        var folderIds = await GetMailFolderIdsAsync(context, dataSourceId, mail.Locations, token);
        var mailEntity = await context.MailMessages.FirstOrDefaultAsync(entity => entity.ParentFileId == mail.MailId, token);
        if (mailEntity is null)
        {
            mailEntity = new MailMessageEntity
            {
                ParentFileId = mail.MailId,
                FirstSeenUtc = mail.FirstSeenUtc,
            };
            context.MailMessages.Add(mailEntity);
        }
        else
        {
            await context.MailAddresses.Where(address => address.ParentFileId == mail.MailId).ExecuteDeleteAsync(token);
            await context.MailParts.Where(part => part.ParentFileId == mail.MailId).ExecuteDeleteAsync(token);
            await context.MailLocations.Where(location => location.ParentFileId == mail.MailId).ExecuteDeleteAsync(token);
        }

        ApplyMail(mailEntity, dataSourceId, mail);
        context.MailAddresses.AddRange(ToMailAddressEntities(mail));
        context.MailParts.AddRange(ToMailPartEntities(mail));
        var displacedMailIds = await ClaimMailLocationsAsync(context, mail.MailId, mail.Locations, folderIds, token);

        await context.SaveChangesAsync(token);
        await OrphanMailsWithoutLocationsAsync(context, displacedMailIds, token);
        await transaction.CommitAsync(token);
    }

    public override async Task<MailRecord?> GetMailAsync(string dataSourceId, string mailId, CancellationToken token)
    {
        await using var context = this.CreateContext();
        var mail = await context.MailMessages
            .AsNoTracking()
            .AsSplitQuery()
            .Include(entity => entity.Addresses)
            .Include(entity => entity.Parts)
            .Include(entity => entity.Locations)
            .ThenInclude(location => location.Folder)
            .FirstOrDefaultAsync(entity => entity.DataSourceId == dataSourceId && entity.ParentFileId == mailId, token);

        return mail is null ? null : ToMailRecord(mail);
    }

    public override async Task<bool> AddMailLocationAsync(string dataSourceId, string mailId, MailLocationRecord location, CancellationToken token)
    {
        await using var context = this.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(token);

        var mailEntity = await context.MailMessages.FirstOrDefaultAsync(entity => entity.DataSourceId == dataSourceId && entity.ParentFileId == mailId, token);
        if (mailEntity is null)
            return false;

        var folderIds = await GetMailFolderIdsAsync(context, dataSourceId, [location], token);
        var displacedMailIds = await ClaimMailLocationsAsync(context, mailId, [location], folderIds, token);
        mailEntity.OrphanedAtUtc = null;

        await context.SaveChangesAsync(token);
        await OrphanMailsWithoutLocationsAsync(context, displacedMailIds, token);
        await transaction.CommitAsync(token);
        return true;
    }

    public override async Task RemoveMailLocationsAsync(string dataSourceId, string folderPath, IReadOnlyCollection<long> uids, CancellationToken token)
    {
        if (uids.Count == 0)
            return;

        await using var context = this.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(token);

        if (await FindMailFolderIdAsync(context, dataSourceId, folderPath, token) is not { } folderId)
            return;

        foreach (var uidBatch in uids.Chunk(CHUNK_UPSERT_BATCH_SIZE))
            await DropMailLocationsAsync(context, context.MailLocations.Where(location => location.FolderId == folderId && uidBatch.Contains(location.Uid)), token);

        await transaction.CommitAsync(token);
    }

    public override async Task<IReadOnlyDictionary<long, MailFlags>> GetMailLocationsAsync(string dataSourceId, string folderPath, CancellationToken token)
    {
        await using var context = this.CreateContext();
        return await context.MailLocations
            .AsNoTracking()
            .Where(location => location.Folder!.DataSourceId == dataSourceId && location.Folder.Path == folderPath)
            .ToDictionaryAsync(location => location.Uid, location => new MailFlags(location.IsSeen, location.IsFlagged, location.IsAnswered), token);
    }

    public override async Task UpdateMailFlagsAsync(string dataSourceId, string folderPath, IReadOnlyDictionary<long, MailFlags> flagsByUid, CancellationToken token)
    {
        if (flagsByUid.Count == 0)
            return;

        await using var context = this.CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(token);

        if (await FindMailFolderIdAsync(context, dataSourceId, folderPath, token) is not { } folderId)
            return;

        foreach (var uidBatch in flagsByUid.Keys.Chunk(CHUNK_UPSERT_BATCH_SIZE))
        {
            token.ThrowIfCancellationRequested();

            var locations = await context.MailLocations
                .Where(location => location.FolderId == folderId && uidBatch.Contains(location.Uid))
                .ToListAsync(token);

            foreach (var location in locations)
            {
                var flags = flagsByUid[location.Uid];
                location.IsSeen = flags.IsSeen;
                location.IsFlagged = flags.IsFlagged;
                location.IsAnswered = flags.IsAnswered;
            }

            await context.SaveChangesAsync(token);
            context.ChangeTracker.Clear();
        }

        await transaction.CommitAsync(token);
    }

    public override async Task<IReadOnlyList<string>> GetOrphanedMailsAsync(string dataSourceId, DateTimeOffset orphanedBefore, CancellationToken token)
    {
        await using var context = this.CreateContext();
        return await context.MailMessages
            .AsNoTracking()
            .Where(mail => mail.DataSourceId == dataSourceId && mail.OrphanedAtUtc != null && mail.OrphanedAtUtc < orphanedBefore)
            .Join(context.EmbeddedFiles, mail => mail.ParentFileId, file => file.ParentFileId, (_, file) => file.AbsolutePath)
            .ToListAsync(token);
    }

    private static void ValidateMail(MailRecord mail)
    {
        if (mail.Locations.Count == 0)
            throw new ArgumentException("A mail is only stored together with at least one place where it lies.", nameof(mail));

        if (mail.Locations.DistinctBy(location => (location.FolderPath, location.Uid)).Count() != mail.Locations.Count)
            throw new ArgumentException("A mail cannot lie at the same place twice.", nameof(mail));

        //
        // The Message-IDs are stored separated by spaces, which a Message-ID can never contain. One
        // which does anyway would come back as two, so it is refused instead of being stored:
        //
        if (mail.ReferenceMessageIds.Any(messageId => messageId.Length == 0 || messageId.Any(char.IsWhiteSpace)))
            throw new ArgumentException("A Message-ID can neither be empty nor contain white space.", nameof(mail));
    }

    private static async Task<int?> FindMailFolderIdAsync(IndexStoreDbContext context, string dataSourceId, string folderPath, CancellationToken token) =>
        await context.MailFolders
            .Where(folder => folder.DataSourceId == dataSourceId && folder.Path == folderPath)
            .Select(folder => (int?)folder.Id)
            .FirstOrDefaultAsync(token);

    private static async Task<IReadOnlyDictionary<string, int>> GetMailFolderIdsAsync(IndexStoreDbContext context, string dataSourceId, IEnumerable<MailLocationRecord> locations, CancellationToken token)
    {
        var folderPaths = locations.Select(location => location.FolderPath).Distinct(StringComparer.Ordinal).ToArray();
        var folderIds = await context.MailFolders
            .Where(folder => folder.DataSourceId == dataSourceId && folderPaths.Contains(folder.Path))
            .ToDictionaryAsync(folder => folder.Path, folder => folder.Id, StringComparer.Ordinal, token);

        //
        // The message names neither the folder nor the mailbox: folder names can tell a lot about
        // a person, and this message may well end up in the log.
        //
        if (folderPaths.Any(folderPath => !folderIds.ContainsKey(folderPath)))
            throw new InvalidOperationException("A mail lies in a folder the index does not hold for its mailbox. A folder has to be stored before the mails in it.");

        return folderIds;
    }

    /// <summary>
    /// Gives a mail the given places, taking them away from any other mail which still holds one.
    /// </summary>
    /// <returns>The ids of the mails which lost a place, to orphan those left without any.</returns>
    private static async Task<IReadOnlyList<string>> ClaimMailLocationsAsync(IndexStoreDbContext context, string mailId, IEnumerable<MailLocationRecord> locations, IReadOnlyDictionary<string, int> folderIds, CancellationToken token)
    {
        var displacedMailIds = new List<string>();
        foreach (var location in locations)
        {
            var folderId = folderIds[location.FolderPath];
            var uid = location.Uid;
            var heldLocations = context.MailLocations.Where(existing => existing.FolderId == folderId && existing.Uid == uid);

            displacedMailIds.AddRange(await heldLocations.Select(existing => existing.ParentFileId).ToListAsync(token));
            await heldLocations.ExecuteDeleteAsync(token);

            context.MailLocations.Add(new MailLocationEntity
            {
                ParentFileId = mailId,
                FolderId = folderId,
                Uid = uid,
                IsSeen = location.Flags.IsSeen,
                IsFlagged = location.Flags.IsFlagged,
                IsAnswered = location.Flags.IsAnswered,
            });
        }

        return displacedMailIds;
    }

    /// <summary>
    /// Removes the given places and orphans the mails which are left without any.
    /// </summary>
    private static async Task DropMailLocationsAsync(IndexStoreDbContext context, IQueryable<MailLocationEntity> locations, CancellationToken token)
    {
        var affectedMailIds = await locations.Select(location => location.ParentFileId).Distinct().ToListAsync(token);
        await locations.ExecuteDeleteAsync(token);
        await OrphanMailsWithoutLocationsAsync(context, affectedMailIds, token);
    }

    private static async Task OrphanMailsWithoutLocationsAsync(IndexStoreDbContext context, IReadOnlyCollection<string> mailIds, CancellationToken token)
    {
        if (mailIds.Count == 0)
            return;

        DateTimeOffset? orphanedAtUtc = DateTimeOffset.UtcNow;
        foreach (var mailIdBatch in mailIds.Distinct(StringComparer.Ordinal).Chunk(CHUNK_UPSERT_BATCH_SIZE))
            await context.MailMessages
                .Where(mail => mailIdBatch.Contains(mail.ParentFileId) && mail.OrphanedAtUtc == null && !mail.Locations.Any())
                .ExecuteUpdateAsync(setters => setters.SetProperty(mail => mail.OrphanedAtUtc, orphanedAtUtc), token);
    }

    private static void ApplyMailFolder(MailFolderEntity folderEntity, MailFolderRecord folder)
    {
        folderEntity.SpecialUse = folder.SpecialUse.ToString();
        folderEntity.UidValidity = folder.UidValidity;
        folderEntity.UidNext = folder.UidNext;
        folderEntity.HighestModSeq = folder.HighestModSeq;
        folderEntity.ServerMessageCount = folder.ServerMessageCount;
        folderEntity.ServerUnseenCount = folder.ServerUnseenCount;
        folderEntity.InitialSyncCompletedUtc = folder.InitialSyncCompletedUtc;
    }

    private static void ApplyMail(MailMessageEntity mailEntity, string dataSourceId, MailRecord mail)
    {
        mailEntity.DataSourceId = dataSourceId;
        mailEntity.MessageId = mail.MessageId;
        mailEntity.InReplyTo = mail.InReplyTo;
        mailEntity.ReferenceMessageIds = string.Join(' ', mail.ReferenceMessageIds);
        mailEntity.SentAtUtc = mail.SentAtUtc;
        mailEntity.ReceivedAtUtc = mail.ReceivedAtUtc;
        mailEntity.Importance = mail.Importance.ToString();
        mailEntity.EncryptionKind = mail.EncryptionKind.ToString();
        mailEntity.MailHash = mail.MailHash;
        mailEntity.OrphanedAtUtc = null;

        if (mail.FirstSeenUtc < mailEntity.FirstSeenUtc)
            mailEntity.FirstSeenUtc = mail.FirstSeenUtc;
    }

    private static IEnumerable<MailAddressEntity> ToMailAddressEntities(MailRecord mail) =>
        NumberWithinGroups(mail.Addresses, address => address.Role).Select(numbered => new MailAddressEntity
        {
            ParentFileId = mail.MailId,
            Role = numbered.Item.Role.ToString(),
            Position = numbered.Position,
            Address = numbered.Item.Address,
            DisplayName = numbered.Item.DisplayName,
        });

    private static IEnumerable<MailPartEntity> ToMailPartEntities(MailRecord mail) =>
        NumberWithinGroups(mail.Parts, part => part.Kind).Select(numbered => new MailPartEntity
        {
            ParentFileId = mail.MailId,
            Kind = numbered.Item.Kind.ToString(),
            Position = numbered.Position,
            Name = numbered.Item.Name,
            ContentType = numbered.Item.ContentType,
            PartSize = numbered.Item.PartSize,
            Text = numbered.Item.Text,
            TextState = numbered.Item.TextState.ToString(),
        });

    /// <summary>
    /// Numbers the items within their group, in the order they come in, starting at zero for each group.
    /// </summary>
    private static IEnumerable<(T Item, int Position)> NumberWithinGroups<T, TGroup>(IEnumerable<T> items, Func<T, TGroup> groupOf) where TGroup : notnull
    {
        var nextPositions = new Dictionary<TGroup, int>();
        foreach (var item in items)
        {
            var group = groupOf(item);
            var position = nextPositions.GetValueOrDefault(group);
            nextPositions[group] = position + 1;
            yield return (item, position);
        }
    }

    private static MailFolderRecord ToMailFolderRecord(MailFolderEntity folder) => new(
        folder.Path,
        ParseStoredName(folder.SpecialUse, MailFolderSpecialUse.UNKNOWN),
        folder.UidValidity,
        folder.UidNext,
        folder.HighestModSeq,
        folder.ServerMessageCount,
        folder.ServerUnseenCount,
        folder.InitialSyncCompletedUtc);

    private static MailRecord ToMailRecord(MailMessageEntity mail) => new(
        mail.ParentFileId,
        mail.MessageId,
        mail.InReplyTo,
        mail.ReferenceMessageIds.Split(' ', StringSplitOptions.RemoveEmptyEntries),
        mail.SentAtUtc,
        mail.ReceivedAtUtc,
        ParseStoredName(mail.Importance, MailImportance.NORMAL),
        ParseStoredName(mail.EncryptionKind, MailEncryptionKind.UNKNOWN),
        mail.MailHash,
        mail.FirstSeenUtc,
        mail.Addresses
            .Select(address => (Role: ParseStoredName(address.Role, MailAddressRole.UNKNOWN), Address: address))
            .OrderBy(entry => entry.Role)
            .ThenBy(entry => entry.Address.Position)
            .Select(entry => new MailAddressRecord(entry.Role, entry.Address.Address, entry.Address.DisplayName))
            .ToList(),
        mail.Parts
            .Select(part => (Kind: ParseStoredName(part.Kind, MailPartKind.UNKNOWN), Part: part))
            .OrderBy(entry => entry.Kind)
            .ThenBy(entry => entry.Part.Position)
            .Select(entry => new MailPartRecord(entry.Kind, entry.Part.Name, entry.Part.ContentType, entry.Part.PartSize, entry.Part.Text, ParseStoredName(entry.Part.TextState, MailPartTextState.UNKNOWN)))
            .ToList(),
        mail.Locations
            .OrderBy(location => location.Folder!.Path, StringComparer.Ordinal)
            .ThenBy(location => location.Uid)
            .Select(location => new MailLocationRecord(location.Folder!.Path, location.Uid, new MailFlags(location.IsSeen, location.IsFlagged, location.IsAnswered)))
            .ToList());

    /// <remarks>
    /// A row written by a newer version may name a value this one does not know. The row still
    /// says everything else it says, so only that one value falls back.
    /// </remarks>
    private static TEnum ParseStoredName<TEnum>(string name, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(name, ignoreCase: true, out var value) && Enum.IsDefined(value) ? value : fallback;
}