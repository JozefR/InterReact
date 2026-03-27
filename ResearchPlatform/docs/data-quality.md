# Data Quality Suite (T-011)

This document describes the first repository-backed QA suite for warehouse data.

## Scope

`T-011` turns the existing `qa_results` table into an active validation pipeline.

The suite currently runs over:

- raw daily prices
- corporate actions
- adjusted daily prices

Results are stored in `qa_results` and linked to an `ingestion_runs` row for auditability.

## Contract Surface

Contracts added under `ResearchPlatform.Contracts`:

- `Abstractions/IDataQualityRepository.cs`
- `Quality/DataQualityRunRequest.cs`
- `Quality/DataQualityRunResult.cs`
- `Quality/DataQualityResultSnapshot.cs`
- `Quality/DataQualitySeverity.cs`
- `Quality/DataQualityStatus.cs`

## Warehouse Implementation

SQLite-backed implementation:

- `src/Modules/DataWarehouse/Quality/EfDataQualityRepository.cs`
- `src/Modules/DataWarehouse/Quality/SqliteDataQualityRepositoryFactory.cs`

The repository:

- creates an `ingestion_runs` entry for each QA execution
- evaluates a fixed set of checks over the requested provider/symbol/date scope
- writes per-check outcomes into `qa_results`
- returns summary counts for passes, failures, warnings, and errors

## Current Checks

### Raw price checks

- `RawPricePresence`
  - fails if no raw rows exist for the requested symbol/range
- `RawPriceShape`
  - validates positive OHLC and valid price ordering
- `UnexplainedPriceJump`
  - flags large close-to-close jumps when no split exists between the two trading dates

### Corporate action checks

- `CorporateActionValues`
  - validates positive split ratios
  - validates non-negative dividend values
  - warns on missing dividend currency
  - warns on non-positive provider adjustment factors when present

### Adjusted series checks

- `AdjustedRowCoverage`
  - ensures adjusted rows exist for every raw trade date for each requested basis
  - flags extra adjusted rows with no matching raw row
- `AdjustedSeriesShape`
  - validates adjusted OHLC positivity/order and positive adjustment factors

## Why These Checks First

These checks were chosen because they are high-value and low-ambiguity before the trading-calendar engine exists.

That means `T-011` intentionally avoids exchange-calendar gap detection for now. A naive weekday-gap check would create false positives around market holidays. That belongs in `T-013`, when the platform has an explicit session calendar.

## Smoke Command

End-to-end smoke:

```bash
dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --qa-smoke
```

Production-profile smoke with Massive fixture mode:

```bash
RP_ENVIRONMENT=Production \
RP__DataIngestion__MassiveUseFixtureFallbackWhenApiKeyMissing=true \
dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --qa-smoke
```

What the smoke does:

1. seeds raw prices, corporate actions, and adjusted series
2. runs a baseline QA pass and expects zero failures
3. injects two anomalies:
   - one missing adjusted row
   - one large unexplained raw price jump
4. reruns QA and expects both anomalies to be reported

## Output Model

Each QA result stores:

- `CheckName`
- `Scope`
- `Severity`
- `Status`
- `AffectedRows`
- `DetailsJson`
- `CreatedUtc`
- linked `IngestionRunId`

This gives the platform a persistent audit trail instead of treating QA as transient console output.

## Current Limitations

- no exchange-calendar-aware gap detection yet
- no stale-adjusted-versus-raw value comparison yet
- no scheduler/alert automation yet; that is `T-012`
