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
- **`IndexedRunContext`** - one prepared run: both stores, the embedding provider, the manifest and the
  collection. `IndexDocumentAsync` embeds and stores one document; the cleanup methods remove what a failed
  attempt left behind.
- **`EmbeddingDocument`** - one document: its key, its index row, its display name, and how to read its chunks.
- **`DocumentRunProgress`** - counts the documents, records indexed and failed ones in the stores, publishes the
  status and completes the run.
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

Rules which are easy to break:
- **A document key is not a path.** Only files use their full path as the key. Never pass a key through the
  `Path` APIs: on Windows, `Path.GetFullPath` reads a key like `mail:…` as a file with an alternate data stream.
- **Ids and signature are pinned.** The formats in `IndexedDocumentIds` and the embedding signature
  (`DataSourceEmbeddingService.BuildEmbeddingSignature`) are fixed by tests, because every stored chunk and every
  index depends on them. When the metadata stored next to a chunk changes, raise `CHUNK_METADATA_VERSION`
  deliberately: that rebuilds every index.
- **The service decides when, the indexer decides how.** Whether changes are tracked at all depends on the
  automatic refresh setting and the startup hash check, and only the embedding service decides that.
