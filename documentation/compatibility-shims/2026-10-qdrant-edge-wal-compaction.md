# Qdrant Edge WAL Compaction

- Status: Active
- Introduced: 2026-10-09
- Remove after: when a qdrant-edge release acknowledges its WAL in `EdgeShard::flush` by itself, see [qdrant/qdrant#11068](https://github.com/qdrant/qdrant/issues/11068)
- Code references:
  - `runtime/src/qdrant_edge_database.rs` (`compact_wal`, `has_obsolete_wal_segments`, `remove_obsolete_wal_segments`, `wal_file_names`, `obsolete_wal_segments`, `optimize_store`)

## User Impact

Qdrant Edge writes every update of a vector store to its write-ahead log (WAL), but never acknowledges the WAL and never replays it. Up to and including qdrant-edge 0.8.0, the `wal` directory of every local data source therefore grows with each indexing run and is never truncated.

On Windows, every WAL segment allocates its full 32 MiB on disk. Users who index often end up with more WAL than embeddings, and loading a store takes longer because Qdrant Edge reads and checks every segment.

## Compatibility Behavior

Right before AI Studio loads a vector store, it removes all closed WAL segments (`closed-<start index>`) except the newest one. It removes them oldest first, so the remaining segments stay contiguous even if a removal fails. Open segments, temporary files, and unknown files stay untouched.

The newest closed segment has to stay: Qdrant Edge numbers its operations from the first closed segment and would start again at zero without one. The segments would then ignore every later update or deletion of a point they already know. Keeping it matches Qdrant's own `prefix_truncate` with `retain_closed = 1`.

This is safe because AI Studio flushes after every update, so the segments already hold everything the WAL contains. A failed cleanup is logged as a warning and does not block loading the store.

After optimizing a store, AI Studio checks for obsolete segments as well. If there are any, it unloads the store, because Windows cannot delete memory-mapped files, and removes them the same way. The next request loads the store again. Without this, the WAL of a long session would keep growing until the next start.

## Removal Checklist

- Check that the new qdrant-edge release acknowledges the WAL in `EdgeShard::flush`, e.g., by calling `ack` with the version that `flush_all` persisted.
- Remove `compact_wal`, `has_obsolete_wal_segments`, `remove_obsolete_wal_segments`, `wal_file_names`, `obsolete_wal_segments`, and the constants `WAL_DIRECTORY` and `CLOSED_WAL_SEGMENT_PREFIX`.
- Remove the `compact_wal` calls from `get_or_create_store` and `get_existing_store`.
- Remove the compaction block at the end of `optimize_store`.
- Expect the test helper `store_with_several_closed_wal_segments` to fail after the update, because the WAL no longer keeps several closed segments. That failure is the signal to remove this shim.
- Remove the tests `obsolete_wal_segments_keeps_the_newest_closed_segment`, `a_reloaded_store_drops_obsolete_wal_segments_and_keeps_its_points`, `an_update_after_wal_compaction_replaces_the_existing_point`, and `optimize_removes_obsolete_wal_segments_of_a_loaded_store`, together with their helpers.
- Consider keeping a variant of `an_update_after_wal_compaction_replaces_the_existing_point` without the shim: it guards the operation numbering across a reload. It was verified to fail when every closed segment is removed.
- Update this document's status to `Removed`.
