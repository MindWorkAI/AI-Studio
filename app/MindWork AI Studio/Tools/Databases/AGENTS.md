# AGENTS.md

These instructions cover the databases in `app/MindWork AI Studio/Tools/Databases/`. They add to the
`AGENTS.md` in the repository root, which applies here as well.

## Databases

AI Studio keeps three databases, addressed through `DatabaseRole`. Local RAG runs on the first two:

- **`VECTOR_STORE`** — Qdrant Edge through the `qdrant-edge` crate, running **in-process inside the
  Rust runtime**. There is no sidecar process, no port 6333 and no Qdrant API key; .NET reaches it
  over the internal runtime API (`/system/qdrant-edge/*`, see `runtime/src/qdrant_edge_database.rs`),
  secured by the same TLS and API token as every other runtime call. One store per data source,
  named `rag_<data source guid>`, holding a single named vector `embedding` per point.
- **`INDEX_STORE`** — SQLite at `<data directory>/databases/sqlite/rag-index.sqlite3`, reached
  through EF Core. It holds the data sources, the file fingerprints, the chunk texts and an FTS5
  index over them, plus the files which permanently failed to index.
- **`PROVENANCE_STORE`** — SQLite at `<data directory>/databases/provenance/provenance.sqlite3`,
  reached through EF Core with migrations of its own. It holds what chats received besides their
  messages, exactly as the AI received it: attached files, tool results, and passages of data
  sources. A chat keeps only the ids. Never put this into the index store: the index can be
  rebuilt from the files and is treated that way, while what a chat received cannot.

When working on these, keep in mind:

- **`GetDisplayInfo()` feeds the information page.** A new diagnostic value belongs in the client
  that knows it, not in `Pages/Information.razor.cs`. The page renders whatever label-value pairs it
  receives and stays free of per-database knowledge.
- **Let every probe in `GetDisplayInfo()` catch its own failure.** When the method throws, the page
  replaces the *entire* block with the fallback client, so one unreadable value costs all the others
  as well.
- **SQLite runs its async methods synchronously.** `Microsoft.Data.Sqlite`, and EF Core on top of it,
  only looks asynchronous: the calling thread does all the work. A statement which may take long, such
  as `VACUUM` or a check of the whole database, runs in `Task.Run` inside the client, as
  `BackupToDirectoryAsync` does. Otherwise a click in the user interface which reaches it freezes the
  whole window until the statement is done.
- **Raw SQL against SQLite goes through `context.Database.GetDbConnection()`**, not through
  `SqlQueryRaw<T>`: that one expects a column named `Value` and wraps the statement, so a `PRAGMA`
  never works with it.
- **A new EF Core migration needs a `[DynamicDependency]`** in the schema migrator of its store,
  `IndexStoreSchemaMigrator` or `ProvenanceStoreSchemaMigrator`, because `PublishTrimmed` is on and
  the migration type would otherwise be trimmed away. The "Schema version"
  line on the information page shows the applied and pending counts, so a forgotten entry becomes
  visible there.
