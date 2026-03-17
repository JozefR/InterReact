# Ingestion Connectors (T-007, T-008, T-009)

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

`ProviderCorporateActionRecord` now also preserves:

- `AdjustmentFactor`
- `AttributesJson`

## Why this split

- Contract DTOs are provider-neutral and suitable for test doubles.
- `ContinuationToken` and `IsComplete` support provider pagination semantics.
- Payloads stay raw (provider symbols/action codes) so normalization can evolve independently.

## Implementations in DataIngestion

`DataIngestion` now includes:

- `Connectors/ProviderDataConnectorFactory.cs`
- `Connectors/Mock/MockProviderDataConnector.cs`
- `Connectors/Massive/MassiveEodProviderDataConnector.cs`
- `Connectors/Massive/MassiveFixturePayloads.cs`

`Mock` connector behavior:

- SP500/SP100 constituent snapshots
- business-day daily bars
- sample corporate actions

`Massive` connector behavior:

- uses Massive Stocks API aggregate bars endpoint for EOD pulls:
  - `/v2/aggs/ticker/{ticker}/range/1/day/{from}/{to}`
- uses Massive Stocks corporate-action endpoints:
  - `/stocks/v1/dividends`
  - `/stocks/v1/splits`
- supports filtered daily price pulls by symbol + date window
- supports filtered corporate-action pulls by symbol + date window
- supports fixture fallback when API key is missing (deterministic local validation)
- preserves raw provider corporate-action metadata in `AttributesJson`

Note:
- Massive connector is still EOD-focused in this phase.
- It intentionally does not implement index constituent snapshots.

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

Run smoke against Massive profile (`Production` config currently points `DataIngestion.Provider` to `Massive`):

```bash
RP_ENVIRONMENT=Production \
  dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --connector-smoke
```
