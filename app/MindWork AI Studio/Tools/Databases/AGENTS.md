# AGENTS.md

These instructions cover the databases in `app/MindWork AI Studio/Tools/Databases/`. They add to the
`AGENTS.md` in the repository root, which applies here as well.

## Databases

Local RAG runs on two databases, addressed through `DatabaseRole`:

- **`VECTOR_STORE`** — Qdrant Edge through the `qdrant-edge` crate, running **in-process inside the
  Rust runtime**. There is no sidecar process, no port 6333 and no Qdrant API key; .NET reaches it
  over the internal runtime API (`/system/qdrant-edge/*`, see `runtime/src/qdrant_edge_database.rs`),
  secured by the same TLS and API token as every other runtime call. One store per data source,
  named `rag_<data source guid>`, holding a single named vector `embedding` per point.
- **`INDEX_STORE`** — SQLite at `<data directory>/databases/sqlite/rag-index.sqlite3`, reached
  through EF Core. It holds the data sources, the file fingerprints, the chunk texts and an FTS5
  index over them, plus the files which permanently failed to index.

When working on these, keep in mind:

- **`GetDisplayInfo()` feeds the information page.** A new diagnostic value belongs in the client
  that knows it, not in `Pages/Information.razor.cs`. The page renders whatever label-value pairs it
  receives and stays free of per-database knowledge.
- **Let every probe in `GetDisplayInfo()` catch its own failure.** When the method throws, the page
  replaces the *entire* block with the fallback client, so one unreadable value costs all the others
  as well.
- **Raw SQL against SQLite goes through `context.Database.GetDbConnection()`**, not through
  `SqlQueryRaw<T>`: that one expects a column named `Value` and wraps the statement, so a `PRAGMA`
  never works with it.
- **A new EF Core migration needs a `[DynamicDependency]`** in `IndexStoreSchemaMigrator`, because
  `PublishTrimmed` is on and the migration type would otherwise be trimmed away. The "Schema version"
  line on the information page shows the applied and pending counts, so a forgotten entry becomes
  visible there.
