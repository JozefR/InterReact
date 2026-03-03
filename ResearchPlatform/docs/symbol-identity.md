# Symbol Identity and Mapping (T-005)

This document describes the symbol identity repository introduced in `T-005`.

## Goal

Provide one canonical symbol registry (`symbol_master`) and deterministic provider-to-canonical mapping enrichment (`symbol_mapping`) for future ingestion pipelines.

## Contract Surface

Added to `ResearchPlatform.Contracts`:

- `ISymbolIdentityRepository`
- `SymbolEnrichmentRequest`
- `SymbolEnrichmentResult`
- `SymbolMasterSnapshot`
- `SymbolMappingSnapshot`
- `AssetType` (contract-level enum)

## DataWarehouse Implementation

Implementation:

- `src/Modules/DataWarehouse/Symbols/EfSymbolIdentityRepository.cs`

Factory:

- `src/Modules/DataWarehouse/Symbols/SqliteSymbolIdentityRepositoryFactory.cs`

## Repository Access Patterns

`ISymbolIdentityRepository` currently supports:

- `UpsertSymbolAsync(...)`
- `GetByCanonicalSymbolAsync(...)`
- `ResolveProviderSymbolAsync(...)`
- `ListActiveSymbolsAsync(...)`
- `ListMappingsAsync(...)`

These patterns intentionally separate write/upsert behavior from read/resolve behavior, so ingestion and research queries can evolve independently.

## Enrichment Rules (Upsert)

`UpsertSymbolAsync` applies these rules:

1. Normalize inputs (`Provider`, `ProviderSymbol`, `CanonicalSymbol`, `ExchangeMic`, `Currency`) to stable casing/format.
2. Upsert canonical symbol metadata in `symbol_master` by canonical symbol key.
3. Resolve an exact mapping key (`Provider`, `ProviderSymbol`, `EffectiveFrom`) if present.
4. If mapping overlaps the new effective date for same provider symbol:
- If it already points to same canonical symbol, reuse it.
- If it starts on same day but points elsewhere, reassign to canonical symbol.
- If it started earlier and overlaps, close it at `EffectiveFrom - 1 day`.
5. Close older overlapping aliases for same canonical symbol/provider when provider symbol changes.
6. Insert new mapping only when no reusable/reassigned mapping exists.

The result reports whether symbol/master rows were created or updated and how many overlapping mappings were closed.

## Smoke Command

`ResearchPlatform.App` includes an opt-in smoke command:

```bash
RP__DataWarehouse__ConnectionString="Data Source=/absolute/path/researchplatform-smoke.db" \
  dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --symbol-smoke
```

The smoke command:

- applies pending migrations for the configured SQLite database
- performs one upsert for `Mock/AAPL`
- resolves provider symbol mapping for current date
- prints summary counters

Use an absolute SQLite path to avoid relative-path differences between tooling and app execution contexts.

## Notes

- This is a research-phase repository abstraction; additional cross-provider identity fields can be added in future tasks.
- `T-006` now builds PIT index constituent loading on top of this identity layer.
