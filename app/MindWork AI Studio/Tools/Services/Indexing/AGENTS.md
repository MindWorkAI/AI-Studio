# AGENTS.md

These instructions cover the indexing pipeline in `app/MindWork AI Studio/Tools/Services/Indexing/` and
`DataSourceEmbeddingService`. They add to the `AGENTS.md` in the repository root, which applies here as
well.

## Indexed data sources

The parts:
- **`IIndexedDataSource`** (`Settings/`) - what indexing needs to know about a data source: confidence level,
  embedding provider and chunk settings. `IDataSourceBase` is what every data source has. Implement
  `IDataSource` on top only when classic RAG, Semantic Search and the agents should see the data source. A data
  source kept in a list of its own implements `IIndexedDataSource` alone, and the compiler keeps it out of
  `DataSources`.
- **`IIndexedSourceIndexer`** - one per kind of data source. `Supports` claims the data sources, `ProcessAsync`
  finds and reads the documents of one run, and `TrackChanges` / `StopTracking` notice changes on their own.
  `FileSourceIndexer` is the reference: a file system watcher per data source, fingerprints over path, size and
  write time.
- **`IndexedRunContext`** - one prepared run: both stores, the embedding provider, the manifest, the
  collection, and the scope for reporting prompt injections. `IndexDocumentAsync` embeds and stores one
  document; the cleanup methods remove what a failed attempt left behind.
- **`EmbeddingDocument`** - one document: its key, its index row, its display name, and how to read its chunks.
- **`DocumentRunProgress`** - counts the documents, records indexed and failed ones in the stores, publishes the
  status, and completes or pauses the run.
- **`RunShare`** - how much one run may do before the other data sources get their turn: five minutes, and for
  a mailbox at most 500 mails. A large data source is indexed in steps, one run each. A run which returns
  `MORE_TO_DO` gets its next step in the back lane of `DataSourceRunQueue`, behind every data source which begins
  a pass, so a mailbox waits for one step at most.
- **`IndexingPass`** - the steps of one data source, from its first step until one of them is done. It remembers
  which documents failed during an earlier step, so the next steps do not read them again, and which failures
  were reported to the user already.
- **`TextChunker`** - cuts text into chunks the embedding provider accepts. Pick one of its strategies; do not
  write a chunker of your own.

To add a kind of data source:
1. Write its indexer in `Tools/Services/Indexing/` and create it in `DataSourceEmbeddingService.CreateIndexers`,
   which hands every indexer the same `TextChunker`.
2. Gate it in `IsSupportedIndexedSource`, behind a preview feature of its own while it is new.
3. When the data source is not kept in `DataSources`, add its list to `GetConfiguredIndexedSources`. Every lookup
   by id and every pass over all data sources goes through it: the startup hash check,
   `QueueAllInternalDataSourcesAsync` and `RefreshWatchers`.
4. Keep whatever the kind has to remember beyond its documents in tables of its own in the index store, added
   by an EF Core migration (see `app/MindWork AI Studio/Tools/Databases/AGENTS.md`).
5. Report every status through `DocumentRunProgress`, so all rows of the embedding page behave alike.
6. Work in steps. Give the indexer its `Share`, and ask `DocumentRunProgress.HasDoneItsShare` before every
   document. Leave out the documents which failed during an earlier step of the pass: either ask
   `SkipIfFailedEarlierInPass` per document, or sort them out up front like `FileSourceIndexer.PartitionFiles`
   and count them with `RecordFailedEarlierInPass`. Once the share is done, return what `PauseRunAsync`
   returns: it decides whether the data source carries on in another run, or stops because its embedding
   provider failed for everything. A run which takes more than twice its share without asking once ends up as
   a warning in the log, while every other data source waits.
7. Filter everything the indexer reads: files through `ExtractedFileText.ReadAsync`, any other content through
   `PromptInjectionGuardService.SanitizeAsync`, both with `IndexedRunContext.PromptInjectionScope`. The service
   opens that scope per run, so the user hears about a run once, when it is over. Begin no action of your own
   in the indexer, or the user hears about one run several times.

Rules which are easy to break:
- **A document key is not a path.** Only files use their full path as the key. Never pass a key through the
  `Path` APIs: on Windows, `Path.GetFullPath` reads a key like `mail:…` as a file with an alternate data stream.
- **Ids and signature are pinned.** The formats in `IndexedDocumentIds` and the embedding signature
  (`DataSourceEmbeddingService.BuildEmbeddingSignature`) are fixed by tests, because every stored chunk and every
  index depends on them. When the metadata stored next to a chunk changes, raise `CHUNK_METADATA_VERSION`
  deliberately: that rebuilds every index.
- **The service decides when, the indexer decides how.** Whether changes are tracked at all depends on the
  automatic refresh setting and the startup hash check, and only the embedding service decides that.
- **A pause stores the source hash only when a partial index may be searched.** A stored source hash tells the
  data source selection that a run got through the whole data source (`IsIndexAwaitingRebuild`). Files pass
  `null` to `PauseRunAsync`, so a folder stays out of the selection until its first indexing or its rebuild is
  complete. The mailbox passes its hash, since every mail it indexed can be searched right away.
- **Every run passes the pause.** A backup pauses the indexing with `PauseIndexingAsync` while it copies the
  stores. Runs wait for the end of a pause in `ProcessDataSourceRunAsync`, in front of the run gate, so start
  every run there. A run which starts any other way writes to the stores while they are copied, and the backup
  holds two stores which do not fit together.
- **A pass lives only while the last step left more to do.** The service drops the pass of a data source after
  every other outcome and every exception, and when the stored index is reset. A retry the user asks for, and a
  step with other embedding settings, begin a new pass (`IndexingPass.ContinueOrBegin`). A pass kept beyond that
  leaves out documents which would work by now, for example once the embedding provider is reachable again.
