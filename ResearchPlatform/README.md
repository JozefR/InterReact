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

## Ingestion Connectors (T-007)

Provider-agnostic ingestion connector contract and mock implementation:

- contracts: `src/Contracts/ResearchPlatform.Contracts/Abstractions/IProviderDataConnector.cs`
- mock connector: `src/Modules/DataIngestion/Connectors/Mock/MockProviderDataConnector.cs`
- details: `docs/ingestion-connectors.md`

Optional smoke run:

```bash
dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --connector-smoke
```
