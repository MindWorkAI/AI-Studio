using System.Globalization;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.OutlookMail;

internal sealed class EwsMailClient : IDisposable
{
    private static readonly XNamespace SOAP = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace M = "http://schemas.microsoft.com/exchange/services/2006/messages";
    private static readonly XNamespace T = "http://schemas.microsoft.com/exchange/services/2006/types";
    private const int MAX_RESPONSE_BYTES = 2_000_000;
    private const int MAX_FOLDERS = 250;
    private const int FOLDER_PAGE_SIZE = 100;
    private const int MAX_PARALLEL_FOLDER_SEARCHES = 4;
    internal const int MAX_RESULTS = 20;
    internal const int MAX_BODY_CHARACTERS = 20_000;

    private readonly HttpClient client;
    private readonly Uri endpoint;

    internal EwsMailClient(Uri endpoint, HttpMessageHandler? handler = null)
    {
        this.endpoint = endpoint;
        this.client = new HttpClient(handler ?? new HttpClientHandler
        {
            UseDefaultCredentials = true,
            AllowAutoRedirect = false,
        });
        this.client.Timeout = TimeSpan.FromSeconds(30);
    }

    internal static bool TryValidateEndpoint(string? value, out Uri endpoint)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0 &&
            uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 &&
            uri.AbsolutePath.EndsWith("/EWS/Exchange.asmx", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = uri;
            return true;
        }

        endpoint = null!;
        return false;
    }

    internal async Task<EwsSearchResult> SearchAsync(string terms, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        var (folders, foldersPartial) = await this.FindFoldersAsync(deadline.Token);

        //
        // Recent mail is most often the mail the user means. Every folder is therefore asked for
        // its newest matches, and the newest of all of them win. Stopping at the first folders
        // with enough hits would return old mail from an archive and leave out yesterday's
        // message in the inbox.
        //
        var resultLock = new Lock();
        var messages = new List<EwsMessage>();
        var partial = foldersPartial;
        var searchedFolders = false;
        try
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = MAX_PARALLEL_FOLDER_SEARCHES, CancellationToken = deadline.Token };
            await Parallel.ForEachAsync(folders, options, async (folder, folderToken) =>
            {
                var folderResult = await this.SearchFolderAsync(folder, terms, folderToken);
                lock (resultLock)
                {
                    messages.AddRange(folderResult.Messages);
                    partial |= folderResult.Partial;
                    searchedFolders |= folderResult.Searched;
                }
            });
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            partial = true;
        }

        if (!searchedFolders && folders.Count > 0)
            throw new EwsMailException("Exchange could not search the primary mailbox. Check the VPN connection and EWS access.");

        if (messages.Count > MAX_RESULTS)
            partial = true;

        var newest = messages.OrderByDescending(message => message.ReceivedAt).Take(MAX_RESULTS).ToList();
        return new EwsSearchResult(newest, partial);
    }

    private async Task<(List<EwsMessage> Messages, bool Partial, bool Searched)> SearchFolderAsync(string folder, string terms, CancellationToken token)
    {
        try
        {
            var root = ResponseRoot(await this.SendAsync(BuildFindItem(folder, terms, MAX_RESULTS), token), "FindItem");
            var items = root.Element(T + "Items") ?? throw new EwsMailException("Exchange returned an incomplete search response.");
            var partial = root.Attribute("IncludesLastItemInRange")?.Value != "true";
            var returned = items.Elements(T + "Message").ToList();
            if (returned.Count > MAX_RESULTS)
                partial = true;

            var messages = new List<EwsMessage>();
            foreach (var item in returned.Take(MAX_RESULTS))
            {
                if (RequiredId(item, "ParentFolderId") != folder)
                {
                    partial = true;
                    continue;
                }

                var id = RequiredId(item, "ItemId");
                var subject = Truncate(item.Element(T + "Subject")?.Value ?? string.Empty, 300);
                var mailbox = item.Element(T + "From")?.Element(T + "Mailbox");
                var sender = Truncate(mailbox?.Element(T + "EmailAddress")?.Value ?? mailbox?.Element(T + "Name")?.Value ?? string.Empty, 320);
                var rawDate = item.Element(T + "DateTimeReceived")?.Value ?? item.Element(T + "DateTimeSent")?.Value ?? string.Empty;
                var receivedAt = DateTimeOffset.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedDate) ? parsedDate : DateTimeOffset.MinValue;
                var excerpt = item.Element(T + "Preview")?.Value ?? item.Element(T + "Body")?.Value ?? string.Empty;
                var webPath = item.Element(T + "WebClientReadFormQueryString")?.Value;
                messages.Add(new EwsMessage(id, subject, sender, Truncate(rawDate, 64), Truncate(excerpt, 500), webPath?.Length <= 2048 ? webPath : null, receivedAt));
            }

            return (messages, partial, true);
        }
        catch (EwsMailException)
        {
            return ([], true, false);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            // The per-request timeout of the HTTP client; the other folders are still searched.
            return ([], true, false);
        }
    }

    internal async Task<EwsReadResult> ReadAsync(string itemId, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        var (folders, partial) = await this.FindFoldersAsync(deadline.Token);
        var root = ResponseRoot(await this.SendAsync(BuildGetItem(itemId), deadline.Token), "GetItem");
        var message = root.Element(M + "Items")?.Element(T + "Message")
            ?? throw new EwsMailException("Exchange did not return a mail message.");
        if (RequiredId(message, "ItemId") != itemId)
            throw new EwsMailException("Exchange returned a different message than the one requested.");
        var parentId = RequiredId(message, "ParentFolderId");
        if (!folders.Contains(parentId, StringComparer.Ordinal))
            throw new EwsMailException(partial
                ? "The primary mailbox folder list is incomplete, so this message cannot be verified."
                : "The message is outside the signed-in user's primary mailbox folders.");

        var bodyElement = message.Element(T + "Body")
            ?? throw new EwsMailException("Exchange did not return a message body.");
        if (bodyElement.Attribute("BodyType")?.Value != "Text")
            throw new EwsMailException("Exchange did not return a plain-text message body.");
        var body = bodyElement.Value;
        return new EwsReadResult(
            Truncate(message.Element(T + "Subject")?.Value ?? string.Empty, 300),
            Truncate(body, MAX_BODY_CHARACTERS),
            body.Length > MAX_BODY_CHARACTERS,
            message.Element(T + "WebClientReadFormQueryString")?.Value is { Length: <= 2048 } webPath ? webPath : null);
    }

    private async Task<(List<string> Folders, bool Partial)> FindFoldersAsync(CancellationToken token)
    {
        var folders = new List<string>();
        var offset = 0;
        while (folders.Count < MAX_FOLDERS)
        {
            var root = ResponseRoot(await this.SendAsync(BuildFindFolder(offset, Math.Min(FOLDER_PAGE_SIZE, MAX_FOLDERS - folders.Count)), token), "FindFolder");
            var page = root.Element(T + "Folders")?.Elements().Select(folder => RequiredId(folder, "FolderId")).ToList()
                ?? throw new EwsMailException("Exchange returned an incomplete folder list.");
            var remaining = MAX_FOLDERS - folders.Count;
            folders.AddRange(page.Take(remaining));
            if (page.Count > remaining)
                return (folders, true);
            if (root.Attribute("IncludesLastItemInRange")?.Value == "true")
                return (folders, false);
            var next = root.Attribute("IndexedPagingOffset")?.Value;
            if (!int.TryParse(next, out var nextOffset) || nextOffset <= offset || page.Count == 0)
                throw new EwsMailException("Exchange returned invalid folder paging information.");
            offset = nextOffset;
        }

        return (folders, true);
    }

    private async Task<XDocument> SendAsync(XDocument request, CancellationToken token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, this.endpoint)
        {
            Content = new StringContent(request.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml"),
        };
        var operation = request.Root!.Element(SOAP + "Body")!.Elements().Single().Name.LocalName;
        message.Headers.TryAddWithoutValidation("SOAPAction", $"\"{M}/{operation}\"");
        HttpResponseMessage response;
        try
        {
            response = await this.client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
        }
        catch (HttpRequestException)
        {
            throw new EwsMailException("Exchange is unavailable. Check the VPN connection and EWS URL.");
        }
        catch (TaskCanceledException) when (!token.IsCancellationRequested)
        {
            throw new EwsMailException("Exchange did not respond before the timeout.");
        }
        using var responseScope = response;
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new EwsMailException("Exchange denied Windows integrated authentication for the signed-in user.");
        if (!response.IsSuccessStatusCode)
            throw new EwsMailException("Exchange is unavailable or rejected the request.");
        if (response.Content.Headers.ContentLength > MAX_RESPONSE_BYTES)
            throw new EwsMailException("Exchange returned a response larger than the allowed limit.");

        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, token)) > 0)
        {
            if (buffer.Length + read > MAX_RESPONSE_BYTES)
                throw new EwsMailException("Exchange returned a response larger than the allowed limit.");
            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        try
        {
            using var reader = XmlReader.Create(buffer, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MAX_RESPONSE_BYTES,
            });
            return XDocument.Load(reader);
        }
        catch (XmlException)
        {
            throw new EwsMailException("Exchange returned malformed XML.");
        }
    }

    private static XElement ResponseRoot(XDocument response, string operation)
    {
        var body = response.Root?.Element(SOAP + "Body");
        var responseMessage = body?.Element(M + operation + "Response")?.Element(M + "ResponseMessages")?.Element(M + operation + "ResponseMessage")
            ?? throw new EwsMailException("Exchange returned an invalid SOAP response.");
        if (responseMessage.Attribute("ResponseClass")?.Value != "Success" || responseMessage.Element(M + "ResponseCode")?.Value != "NoError")
            throw new EwsMailException("Exchange could not complete the mail request.");
        return responseMessage.Element(M + "RootFolder") ?? responseMessage;
    }

    private static string RequiredId(XElement parent, string elementName) =>
        parent.Element(T + elementName)?.Attribute("Id")?.Value is { Length: > 0 and <= 4096 } id
            ? id
            : throw new EwsMailException("Exchange omitted an expected identifier.");

    private static XDocument Envelope(XElement operation) => new(
        new XElement(SOAP + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", SOAP),
            new XAttribute(XNamespace.Xmlns + "m", M),
            new XAttribute(XNamespace.Xmlns + "t", T),
            new XElement(SOAP + "Header", new XElement(T + "RequestServerVersion", new XAttribute("Version", "Exchange2013"))),
            new XElement(SOAP + "Body", operation)));

    internal static XDocument BuildFindFolder(int offset, int pageSize) => Envelope(new XElement(M + "FindFolder",
        new XAttribute("Traversal", "Deep"),
        new XElement(M + "FolderShape", new XElement(T + "BaseShape", "IdOnly")),
        new XElement(M + "IndexedPageFolderView", new XAttribute("MaxEntriesReturned", pageSize), new XAttribute("Offset", offset), new XAttribute("BasePoint", "Beginning")),
        new XElement(M + "ParentFolderIds", new XElement(T + "DistinguishedFolderId", new XAttribute("Id", "msgfolderroot")))));

    internal static XDocument BuildFindItem(string folderId, string terms, int pageSize) => Envelope(new XElement(M + "FindItem",
        new XAttribute("Traversal", "Shallow"),
        new XElement(M + "ItemShape", new XElement(T + "BaseShape", "IdOnly"),
            Properties("item:ParentFolderId", "item:Subject", "item:DateTimeReceived", "item:DateTimeSent", "item:Preview", "message:From", "item:WebClientReadFormQueryString")),
        new XElement(M + "IndexedPageItemView", new XAttribute("MaxEntriesReturned", pageSize), new XAttribute("Offset", 0), new XAttribute("BasePoint", "Beginning")),
        new XElement(M + "SortOrder", new XElement(T + "FieldOrder", new XAttribute("Order", "Descending"), new XElement(T + "FieldURI", new XAttribute("FieldURI", "item:DateTimeReceived")))),
        // The EWS schema requires this order: SortOrder, then ParentFolderIds, then QueryString.
        new XElement(M + "ParentFolderIds", new XElement(T + "FolderId", new XAttribute("Id", folderId))),
        new XElement(M + "QueryString", terms)));

    internal static XDocument BuildGetItem(string itemId) => Envelope(new XElement(M + "GetItem",
        new XElement(M + "ItemShape", new XElement(T + "BaseShape", "IdOnly"), new XElement(T + "BodyType", "Text"),
            Properties("item:ParentFolderId", "item:Subject", "item:Body", "item:WebClientReadFormQueryString")),
        new XElement(M + "ItemIds", new XElement(T + "ItemId", new XAttribute("Id", itemId)))));

    private static XElement Properties(params string[] paths) => new(T + "AdditionalProperties", paths.Select(path => new XElement(T + "FieldURI", new XAttribute("FieldURI", path))));

    internal static string Truncate(string text, int maxCharacters)
    {
        if (text.Length <= maxCharacters)
            return text;
        return text[..(char.IsHighSurrogate(text[maxCharacters - 1]) ? maxCharacters - 1 : maxCharacters)];
    }

    public void Dispose() => this.client.Dispose();
}

internal sealed record EwsMessage(string Id, string Subject, string Sender, string Date, string Excerpt, string? WebPath, DateTimeOffset ReceivedAt);
internal sealed record EwsSearchResult(IReadOnlyList<EwsMessage> Messages, bool Partial);
internal sealed record EwsReadResult(string Subject, string Body, bool Truncated, string? WebPath);
internal sealed class EwsMailException(string message) : Exception(message);
