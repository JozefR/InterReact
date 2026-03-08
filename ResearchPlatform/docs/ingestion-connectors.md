# Ingestion Connectors (T-007, T-008)

This document defines the provider-agnostic ingestion connector contract layer.

## Goal

Provide one stable contract surface for pulling raw market data from any provider adapter without coupling the rest of the system to provider-specific SDKs.

## Contract Surface

Main abstraction:

- `src/Contracts/ResearchPlatform.Contracts/Abstractions/IProviderDataConnector.cs`

DTO families:

- constituent snapshots:
  - `ProviderConstituentSnapshotRequest`
  - `ProviderConstituentRecord`
  - `ProviderConstituentSnapshotBatch`
- daily prices:
  - `ProviderDailyPriceRequest`
  - `ProviderDailyPriceRecord`
  - `ProviderDailyPriceBatch`
- corporate actions:
  - `ProviderCorporateActionRequest`
  - `ProviderCorporateActionRecord`
  - `ProviderCorporateActionBatch`
- connector metadata:
  - `IngestionConnectorCapabilities`

## Why this split

- Contract DTOs are provider-neutral and suitable for test doubles.
- `ContinuationToken` and `IsComplete` support provider pagination semantics.
- Payloads stay raw (provider symbols/action codes) so normalization can evolve independently.

## Implementations in DataIngestion

`DataIngestion` now includes:

- `Connectors/ProviderDataConnectorFactory.cs`
- `Connectors/Mock/MockProviderDataConnector.cs`
- `Connectors/Iex/IexProviderDataConnector.cs`
- `Connectors/Iex/IexFixturePayloads.cs`

`Mock` connector behavior:

- SP500/SP100 constituent snapshots
- business-day daily bars
- sample corporate actions

`IEX` connector behavior (first provider adapter in T-008):

- parses IEX-shaped fixture payloads into contract DTOs
- supports SP500/SP100 constituents with continuation token pagination (`page:<n>`)
- supports filtered daily price pulls by symbol + date window
- supports filtered corporate actions by symbol + date window

Note:
- Current IEX adapter is intentionally fixture-backed for deterministic research/dev validation.
- Swapping fixture payloads for live HTTP transport is a follow-up evolution, not a contract change.

## Smoke Command

Run connector contract smoke test from composition:

```bash
dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --connector-smoke
```

Expected output includes:

- resolved provider connector
- capabilities
- constituent snapshot row count (and pagination state)
- daily price row count
- corporate action row count

Run smoke against IEX profile (`Production` config currently points `DataIngestion.Provider` to `IEX`):

```bash
RP_ENVIRONMENT=Production \
  dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --connector-smoke
```
