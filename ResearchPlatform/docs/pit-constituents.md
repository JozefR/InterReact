# PIT Constituents Pipeline (T-006)

This document describes the point-in-time (PIT) index constituent loading flow for `SP500` and `SP100`.

## Goal

Load and query index memberships in a way that is historically correct:

- no survivorship leakage
- no look-ahead leakage
- deterministic as-of universe reconstruction

## Contract Surface

Added in `ResearchPlatform.Contracts`:

- `IIndexConstituentPitRepository`
- `UniverseCodes`
- `IndexConstituentInput`
- `IndexConstituentSnapshotLoadRequest`
- `IndexConstituentSnapshotLoadResult`
- `IndexConstituentMembershipSnapshot`

## DataWarehouse Implementation

Implementation:

- `src/Modules/DataWarehouse/Constituents/EfIndexConstituentPitRepository.cs`

Factory:

- `src/Modules/DataWarehouse/Constituents/SqliteIndexConstituentPitRepositoryFactory.cs`

## Snapshot Upsert Behavior

`UpsertSnapshotAsync(...)` receives one snapshot for one index code on one effective date.

Behavior:

1. Validate `IndexCode` (`SP500` or `SP100`) and normalize input symbols.
2. Resolve canonical symbols in `symbol_master`.
3. Fail fast if any canonical symbol is missing.
4. Read active rows (`effective_from <= date <= effective_to/null`) for that index.
5. Close active members not present in the new snapshot (set `effective_to = date - 1 day`).
6. Insert members present in snapshot but not currently active.
7. Update active row metadata (source/weight) when changed.

## Query Patterns

- `GetConstituentsAsOfAsync(indexCode, asOfDate)`
  - returns the membership set valid on that date.
- `GetConstituentHistoryAsync(indexCode, canonicalSymbol)`
  - returns all effective windows for that symbol in the index.

## Composition Smoke Command

`ResearchPlatform.App` provides:

```bash
dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --pit-smoke
```

Smoke command flow:

1. Ensures SQLite schema is migrated.
2. Seeds canonical symbols (`AAPL`, `MSFT`, `NVDA`, `AMZN`).
3. Loads two snapshots each for `SP500` and `SP100`.
4. Prints as-of memberships and one symbol history window check.

## Why this matters for backtests

Backtests need the true index membership as-of each historical day.
Using a PIT table avoids a common error: applying today’s constituents to past dates.
