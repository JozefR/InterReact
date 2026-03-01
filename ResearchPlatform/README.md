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
