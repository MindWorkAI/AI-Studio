# Qdrant Edge Tracker Journal

- Status: Active
- Introduced: 2026-11-25
- Remove after: 2027-05-25, once organizations no longer go back to AI Studio v26.11.1 or older, the last versions with qdrant-edge 0.8
- Code references:
  - `runtime/src/qdrant_edge_database.rs` (`remove_tracker_journals`, `tracker_journals`, `TRACKER_JOURNAL_FILE`, `unload_store`, `Drop for QdrantEdgeDatabase`)

## User Impact

Since qdrant-edge 0.9.0, every storage of a vector store notes its pointer writes in the file `tracker_journal.dat` before it writes them, and applies this journal again whenever it opens, which repairs pointers a crash tore during a flush. After a flush, Qdrant Edge keeps the journal until it holds about 1,000 writes. qdrant-edge 0.8.0, which AI Studio v26.11.1 and older use, knows no journal and leaves it alone.

Someone may go back from a newer version of AI Studio to v26.11.1 or older, e.g., because their organization rolls back an update, or because a developer runs a development build and the installed release on the same data directory. When the older version then writes to a store, e.g., while it indexes a changed file, the journal still holds the pointers from before. The next load under qdrant-edge 0.9 sets them again, so they point into freed blocks. In a test with a real store of 318 MB, loading the store then failed with "Failed to decompress LZ4 value", or Qdrant Edge panicked while it applied the WAL. Either way, the store is lost until its embeddings are rebuilt.

## Compatibility Behavior

Whenever AI Studio unloads a store, it flushes it, closes it, and then removes every `tracker_journal.dat` below the store's directory. AI Studio unloads every store when it quits, installs an update, or restarts after restoring a backup, and it unloads a store before copying it into a backup, so backups contain no journal either. After a successful flush, the journal has nothing left to repair. When the flush fails, AI Studio keeps the store loaded and leaves its journals in place, since Qdrant Edge then still needs them.

When the operating system ends AI Studio without letting it quit, the journals stay. The next start of a version with qdrant-edge 0.9 applies them, which repairs what the crash may have torn, and AI Studio removes the journals again when it unloads the store. Only going back to an older version right after such a hard end can still break a store.

In the test, removing the journals before the older version wrote to the store, or before the newer version loaded it again, kept all payloads, filters, and points intact.

## Removal Checklist

- Remove `remove_tracker_journals`, `tracker_journals`, and the constant `TRACKER_JOURNAL_FILE`.
- Remove the call of `remove_tracker_journals` from `unload_store`. Keep `unload_store` and `Drop for QdrantEdgeDatabase` only if they still do more than flushing and closing the stores, which dropping a shard does by itself.
- Remove the tests `unloading_a_store_removes_its_tracker_journals_and_keeps_its_points` and `a_backup_copies_a_store_without_its_tracker_journals`.
- Update this document's status to `Removed`.