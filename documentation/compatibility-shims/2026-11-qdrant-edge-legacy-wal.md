# Qdrant Edge Legacy WAL

- Status: Active
- Introduced: 2026-11-24
- Remove after: when a qdrant-edge release skips the operations of the WAL which its segments already persisted when it loads a store, see [qdrant/qdrant#11732](https://github.com/qdrant/qdrant/issues/11732)
- Code references:
  - `runtime/src/qdrant_edge_database.rs` (`acknowledge_legacy_wal`, `WAL_FIRST_INDEX_FILE`, `WAL_FIRST_INDEX_FILE_TEMP`)

## User Impact

Up to qdrant-edge 0.8.0, Qdrant Edge wrote every update of a vector store to its write-ahead log (WAL), but never acknowledged the WAL. Since 0.9.0, loading a store applies every operation of the WAL again which is not marked as acknowledged. The first load of a store written by an older version of AI Studio therefore applies its whole WAL once more.

That WAL holds thousands of operations. Each deletion of a document in there reads and parses every payload of the store, because the fields it filters on have no payload index. For a real store of 317 MB with 20,316 operations in its WAL, the first load had not finished after 39 minutes. Meanwhile, every request to the vector database waits, and when the user quits AI Studio before the load is done, the next start begins again.

## Compatibility Behavior

Right before AI Studio loads a vector store, it checks whether the WAL directory of the store holds the file `first-index`, in which Qdrant Edge notes how far its WAL is acknowledged. A store of an older version lacks it, because Qdrant Edge 0.9 writes it only with the first flush after an update; so does a new store before its first update, whose WAL holds nothing to apply anyway. For such a store, AI Studio writes the file with the highest possible index, `{"ack_index":18446744073709551615}`, through a temporary file. Qdrant Edge lowers this index to the last entry of the WAL when it opens the WAL, so loading the store applies only that one operation once more.

This is safe because AI Studio flushes after every update, so the segments already hold everything the WAL contains, and because qdrant-edge 0.8 never applied its WAL anyway. A failed write is logged as a warning and does not block loading the store; the load then takes long, but nothing gets lost. The file has the same format in qdrant-edge 0.8 and 0.9, so an older version of AI Studio still opens the store.

Since AI Studio v26.11.2, every store also gets payload indexes on the fields which these deletions filter on (`ensure_payload_indexes`). A store of an older version gets them only once Qdrant Edge has loaded it and applied its WAL, though, so the indexes do not replace this shim.

## Removal Checklist

- Check that the new qdrant-edge release no longer applies operations which the segments already persisted, e.g., by starting the WAL replay at the highest version the segments persisted.
- Remove `acknowledge_legacy_wal` and the constants `WAL_FIRST_INDEX_FILE` and `WAL_FIRST_INDEX_FILE_TEMP`, unless the tests still need them.
- Remove the `acknowledge_legacy_wal` calls from `get_or_create_store` and `get_existing_store`.
- Remove the test `a_store_of_an_older_version_gets_its_wal_acknowledged_and_keeps_its_points`, and keep `an_acknowledged_wal_stays_as_it_is` only if it still checks something of AI Studio.
- Update this document's status to `Removed`.