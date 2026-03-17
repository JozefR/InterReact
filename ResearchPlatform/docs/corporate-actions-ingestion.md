# Corporate Actions Ingestion (T-009)

This document describes the first end-to-end corporate-actions ingestion path: provider fetch, canonical symbol resolution, warehouse upsert, and ingestion-run audit tracking.

## Scope

Implemented in `T-009`:

- provider-side corporate action fetches through `IProviderDataConnector`
- `Massive` support for:
  - `GET /stocks/v1/dividends`
  - `GET /stocks/v1/splits`
- warehouse persistence through `ICorporateActionRepository`
- ingestion-run audit rows for every corporate-action load
- smoke command for full-path validation

Not in scope yet:

- adjusted-price series generation (`T-010`)
- QA checks over action consistency (`T-011`)
- scheduler/retry automation (`T-012`)

## Contract Surface

Repository abstraction:

- `src/Contracts/ResearchPlatform.Contracts/Abstractions/ICorporateActionRepository.cs`

Contract records:

- `CorporateActionLoadRequest`
- `CorporateActionLoadResult`
- `CorporateActionSnapshot`

Connector DTO extension:

- `ProviderCorporateActionRecord` now preserves:
  - `AdjustmentFactor`
  - `AttributesJson`

Why the connector DTO changed:

- `Value` alone is not enough for later price-adjustment work.
- Massive exposes extra event metadata such as `historical_adjustment_factor`, `distribution_type`, `split_from`, and `split_to`.
- `AttributesJson` preserves the provider event payload for future derivations without forcing provider-specific columns into the contract layer immediately.

## Warehouse Changes

`corporate_actions` now stores:

- `AdjustmentFactor` (nullable)
- `RelatedProviderSymbol` (nullable)
- `AttributesJson` (nullable)
- `IngestionRunId` (nullable FK -> `ingestion_runs`)

Migration:

- `src/Modules/DataWarehouse/Schema/Migrations/20260317125628_AddCorporateActionAuditColumns.cs`

## Upsert Rules

`EfCorporateActionRepository`:

- creates one `ingestion_runs` row per load request
- resolves each provider symbol through `symbol_mapping` on the action date
- maps raw provider action codes into canonical warehouse types
- upserts actions by:
  - provider `ExternalId` when available
  - otherwise natural key: `(SymbolMasterId, ActionDate, ActionType, Provider, Value)`
- marks the ingestion run as:
  - `Succeeded`
  - `Failed`
  - `Cancelled`

Failure behavior:

- unresolved provider symbols fail the load
- duplicate batch rows fail the load
- failed loads still keep the ingestion-run ledger row with the error recorded

## Massive Mapping Notes

Dividend mapping:

- date: `ex_dividend_date`
- type code:
  - `DIVIDEND`
  - `SPECIAL_DIVIDEND`
  - `SUPPLEMENTAL_DIVIDEND`
  - `IRREGULAR_DIVIDEND`
- value: `cash_amount`
- adjustment factor: `historical_adjustment_factor`
- attributes: raw provider row JSON

Split mapping:

- date: `execution_date`
- type code:
  - `FORWARD_SPLIT`
  - `REVERSE_SPLIT`
  - `STOCK_DIVIDEND`
- value: `split_to / split_from`
- adjustment factor: `historical_adjustment_factor`
- attributes: raw provider row JSON

## Smoke Validation

Composition command:

```bash
dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --corporate-actions-smoke
```

What it validates:

- connector fetches action rows
- provider symbols resolve into canonical symbols
- warehouse rows are inserted/updated
- `ingestion_runs` row is created
- stored actions can be queried back per canonical symbol

Massive fixture-mode smoke:

```bash
RP_ENVIRONMENT=Production \
  dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --corporate-actions-smoke
```

## Next Dependency

`T-010` should build adjusted/unadjusted series generation on top of:

- `prices_daily_raw`
- `corporate_actions`
- `AdjustmentFactor`
- `AttributesJson` for provider-specific event details
