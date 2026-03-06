# Ingestion Connector Contracts (T-007)

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

## Reference Implementation in DataIngestion

`DataIngestion` now includes:

- `Connectors/ProviderDataConnectorFactory.cs`
- `Connectors/Mock/MockProviderDataConnector.cs`

The mock connector returns deterministic datasets for:

- SP500/SP100 constituent snapshots
- business-day daily bars
- sample corporate actions

## Smoke Command

Run connector contract smoke test from composition:

```bash
dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --connector-smoke
```

Expected output includes:

- resolved provider connector
- capabilities
- constituent snapshot row count
- daily price row count
- corporate action row count
