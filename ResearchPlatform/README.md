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
