# Runtime Patches

This directory documents temporary patches for third-party Rust dependencies.

## permutation_iterator

`qdrant-edge` depends on `permutation_iterator 0.1.2`, and that crate has seen no release since
2019. Its published version pulls in an outdated `rand` line, which drags a second copy of the
whole `rand` family into our dependency tree: `rand 0.7.3`, `rand_core 0.5.1`, `rand_chacha 0.2`,
`rand_hc`, `getrandom 0.1.16`, `wasi 0.9` and `cfg-if 0.1`, next to the current ones everything
else uses.

The fork `SommerEngineering/permutation-iterator-rs` is the published 0.1.2 with `rand` raised to
0.8, so it still satisfies what `qdrant-edge` asks for. AI Studio pins it in `runtime/Cargo.toml`:

```toml
[patch.crates-io]
permutation_iterator = { git = "https://github.com/SommerEngineering/permutation-iterator-rs.git", rev = "..." }
```

The same change was offered upstream in
[asimihsan/permutation-iterator-rs#14](https://github.com/asimihsan/permutation-iterator-rs/pull/14),
where it has been waiting since 2021. This is tree hygiene, not a build failure: without the patch
the runtime still builds, it just carries the old `rand` family along.

### When this patch can go

Either of these is enough, and both are worth a look whenever `qdrant-edge` is updated:

- crates.io carries a `permutation_iterator` newer than 0.1.2 which uses a current `rand`. Then the
  `[patch.crates-io]` entry goes, and `qdrant-edge`'s own requirement decides the version.
- `qdrant-edge` stops depending on `permutation_iterator` at all. Check with
  `cargo tree -i permutation_iterator` after the update.

Afterward, `grep 'name = "rand"' -A 2 runtime/Cargo.lock` must not show a 0.7 version anymore.
`SommerEngineering/permutation-iterator-rs` can then be deleted.
