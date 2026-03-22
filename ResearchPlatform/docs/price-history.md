# Price History and Adjustments (T-010)

This document describes the raw daily price persistence and adjusted-series rebuild path.

## Scope

`T-010` adds two warehouse capabilities:

- persist provider daily OHLCV bars into `prices_daily_raw`
- rebuild adjusted daily series into `prices_daily_adjusted`

Current supported adjustment bases:

- `SplitOnly`
- `SplitAndDividend`

## Contract Surface

Contracts added under `ResearchPlatform.Contracts`:

- `Abstractions/IPriceHistoryRepository.cs`
- `Prices/AdjustmentBasisCodes.cs`
- `Prices/DailyPriceLoadRequest.cs`
- `Prices/DailyPriceLoadResult.cs`
- `Prices/AdjustedPriceBuildRequest.cs`
- `Prices/AdjustedPriceBuildResult.cs`
- `Prices/RawDailyPriceSnapshot.cs`
- `Prices/AdjustedDailyPriceSnapshot.cs`

## Warehouse Implementation

SQLite-backed implementation:

- `src/Modules/DataWarehouse/Prices/EfPriceHistoryRepository.cs`
- `src/Modules/DataWarehouse/Prices/SqlitePriceHistoryRepositoryFactory.cs`

Responsibilities:

- resolve provider symbols to canonical symbols on each trade date
- upsert raw daily bars by `(SymbolMasterId, TradeDate, Provider)`
- rebuild adjusted rows by `(SymbolMasterId, TradeDate, Provider, AdjustmentBasis)`
- write ingestion-run audit rows for raw loads and rebuild runs

## Adjustment Algorithm

Adjusted prices are rebuilt from persisted raw bars plus stored corporate actions.

High-level flow:

1. Load raw bars for each requested symbol from `FromDate` through the latest available trade date.
2. Load corporate actions for the same symbol/provider range.
3. Traverse raw bars in descending trade-date order.
4. Emit the current row using the current cumulative price/volume factors.
5. Apply any corporate actions whose action date falls between the current trading row and the next older trading row.
6. Continue until the full requested range is rebuilt.

Why the action window matters:

- splits and dividends can occur on non-trading days
- applying only `actionDate == tradeDate` would miss weekend/holiday events

Current factor rules:

- split:
  - price factor multiplies by `1 / splitRatio`
  - volume factor multiplies by `splitRatio`
- dividend (`SplitAndDividend` only):
  - price factor multiplies by `(priorClose - cashDividend) / priorClose`
  - volume is not adjusted for cash dividends

## Massive Connector Note

For `T-010`, the Massive connector must feed raw history, not provider-adjusted history.

The connector now requests:

- `adjusted=false` for daily aggregate bars

This keeps `prices_daily_raw` genuinely raw so adjustment logic is owned by the platform.

## SQLite Constraint Note

SQLite stores EF decimal values as `TEXT` in this schema. Plain check constraints such as `High >= Low` can therefore become unsafe when values cross digit boundaries after adjustments.

Example failure mode:

- `100.600000` vs `98.945000`

Lexicographically, `"100.600000"` can compare as smaller than `"98.945000"` even though numerically it is larger.

To avoid false failures, raw and adjusted price-order constraints now cast to numeric explicitly:

- `CAST(Open AS REAL)`
- `CAST(High AS REAL)`
- `CAST(Low AS REAL)`
- `CAST(Close AS REAL)`
- `CAST(AdjustedClose AS REAL)`
- `CAST(AdjustmentFactor AS REAL)`

Application-side validation was also added before adjusted rows are saved so constraint failures surface with more context.

## Smoke Command

End-to-end smoke:

```bash
dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --price-history-smoke
```

Production-profile smoke with Massive fixture mode:

```bash
RP_ENVIRONMENT=Production \
RP__DataIngestion__MassiveUseFixtureFallbackWhenApiKeyMissing=true \
dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --price-history-smoke
```

## Current Limitations

- dividend adjustment uses prior raw close; no total-return benchmark layer exists yet
- same-day multi-action sequencing remains a future QA focus
- live Massive validation still depends on running with a real API key outside this sandbox
