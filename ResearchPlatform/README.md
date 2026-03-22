# ResearchPlatform

Mono-repo scaffold for a long-only US equities research platform.

## Modules

- `src/Contracts/ResearchPlatform.Contracts`
- `src/Modules/DataIngestion`
- `src/Modules/DataWarehouse`
- `src/Modules/BacktestEngine`
- `src/Modules/StrategyRegistry`
- `src/Modules/ExperimentRunner`
- `src/Modules/MetricsReporting`
- `src/Modules/AiResearchAssistant`
- `src/Composition/ResearchPlatform.App`

## Boundary policy

Modules are isolated and may depend only on `ResearchPlatform.Contracts`.
Composition is handled in `ResearchPlatform.App`.

See `docs/module-boundaries.md` for the full rule set.

## Build

```bash
dotnet build ResearchPlatform.sln
```

## Run composition root

```bash
dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj
```

## Configuration

Environment-aware settings are loaded from:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. `RP__...` environment variable overrides

Environment resolution order:

1. `--environment <Name>` argument
2. `RP_ENVIRONMENT`
3. `DOTNET_ENVIRONMENT`
4. `ASPNETCORE_ENVIRONMENT`
5. default `Development`

Validate all configured environments:

```bash
./scripts/validate-config.sh
```

Fast JSON structure validation:

```bash
./scripts/validate-config-json.sh
```

Validate one environment:

```bash
RP_ENVIRONMENT=Test dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --validate-config
```

## CI

Run the same checks locally that CI runs:

```bash
./scripts/ci-validate.sh
```

Workflow:

- `.github/workflows/ci.yml`

## Data Schema and Migrations

Canonical warehouse schema docs:

- `docs/data-schema.md`
- `docs/price-history.md`
- `docs/symbol-identity.md`
- `docs/pit-constituents.md`
- `docs/ingestion-connectors.md`

Apply migrations locally:

```bash
./scripts/db-migrate.sh
```

First-time local tool setup (already committed via tool manifest):

```bash
dotnet tool restore
```

## Symbol Identity (T-005)

Symbol master/mapping enrichment contract and implementation:

- contracts: `src/Contracts/ResearchPlatform.Contracts/Abstractions/ISymbolIdentityRepository.cs`
- implementation: `src/Modules/DataWarehouse/Symbols/EfSymbolIdentityRepository.cs`
- details: `docs/symbol-identity.md`

Optional smoke run:

```bash
ABS_DB="$(pwd)/researchplatform-smoke.db"
RP__DataWarehouse__ConnectionString="Data Source=${ABS_DB}" \
  dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --symbol-smoke
```

## PIT Constituents (T-006)

PIT snapshot loader contract and implementation:

- contracts: `src/Contracts/ResearchPlatform.Contracts/Abstractions/IIndexConstituentPitRepository.cs`
- implementation: `src/Modules/DataWarehouse/Constituents/EfIndexConstituentPitRepository.cs`
- details: `docs/pit-constituents.md`

Optional smoke run:

```bash
dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --pit-smoke
```

## Ingestion Connectors (T-007, T-008, T-009)

Provider-agnostic ingestion connector contract with two adapters:

- contracts: `src/Contracts/ResearchPlatform.Contracts/Abstractions/IProviderDataConnector.cs`
- mock connector: `src/Modules/DataIngestion/Connectors/Mock/MockProviderDataConnector.cs`
- Massive EOD connector: `src/Modules/DataIngestion/Connectors/Massive/MassiveEodProviderDataConnector.cs`
- details: `docs/ingestion-connectors.md`

Optional smoke run:

```bash
dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --connector-smoke
```

Run connector smoke with `Massive` provider profile:

```bash
RP_ENVIRONMENT=Production \
  dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --connector-smoke
```

## Corporate Actions Ingestion (T-009)

Corporate-action persistence and ingestion-run audit path:

- contracts:
  - `src/Contracts/ResearchPlatform.Contracts/Abstractions/ICorporateActionRepository.cs`
  - `src/Contracts/ResearchPlatform.Contracts/CorporateActions/CorporateActionLoadRequest.cs`
  - `src/Contracts/ResearchPlatform.Contracts/CorporateActions/CorporateActionLoadResult.cs`
  - `src/Contracts/ResearchPlatform.Contracts/CorporateActions/CorporateActionSnapshot.cs`
- implementation:
  - `src/Modules/DataWarehouse/CorporateActions/EfCorporateActionRepository.cs`
  - `src/Modules/DataWarehouse/CorporateActions/SqliteCorporateActionRepositoryFactory.cs`
- details:
  - `docs/corporate-actions-ingestion.md`
  - `docs/data-schema.md`

Optional end-to-end persistence smoke:

```bash
dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --corporate-actions-smoke
```

Run the same smoke against the `Massive` provider profile:

```bash
RP_ENVIRONMENT=Production \
  dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --corporate-actions-smoke
```

## Price History and Adjustments (T-010)

Raw daily price persistence plus adjusted-series rebuild path:

- contracts:
  - `src/Contracts/ResearchPlatform.Contracts/Abstractions/IPriceHistoryRepository.cs`
  - `src/Contracts/ResearchPlatform.Contracts/Prices/AdjustmentBasisCodes.cs`
  - `src/Contracts/ResearchPlatform.Contracts/Prices/DailyPriceLoadRequest.cs`
  - `src/Contracts/ResearchPlatform.Contracts/Prices/DailyPriceLoadResult.cs`
  - `src/Contracts/ResearchPlatform.Contracts/Prices/AdjustedPriceBuildRequest.cs`
  - `src/Contracts/ResearchPlatform.Contracts/Prices/AdjustedPriceBuildResult.cs`
  - `src/Contracts/ResearchPlatform.Contracts/Prices/RawDailyPriceSnapshot.cs`
  - `src/Contracts/ResearchPlatform.Contracts/Prices/AdjustedDailyPriceSnapshot.cs`
- implementation:
  - `src/Modules/DataWarehouse/Prices/EfPriceHistoryRepository.cs`
  - `src/Modules/DataWarehouse/Prices/SqlitePriceHistoryRepositoryFactory.cs`
- details:
  - `docs/price-history.md`
  - `docs/data-schema.md`

Optional end-to-end smoke:

```bash
dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --price-history-smoke
```

Run the same smoke against the `Massive` production profile in fixture mode:

```bash
RP_ENVIRONMENT=Production \
RP__DataIngestion__MassiveUseFixtureFallbackWhenApiKeyMissing=true \
  dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --price-history-smoke
```
