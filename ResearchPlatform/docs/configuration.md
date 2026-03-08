# Configuration and Environment Management

## Environment selection priority

The app resolves environment in this order:

1. `--environment <Name>` command argument
2. `RP_ENVIRONMENT`
3. `DOTNET_ENVIRONMENT`
4. `ASPNETCORE_ENVIRONMENT`
5. fallback: `Development`

Supported values: `Development`, `Test`, `Production`.

## Configuration layering

At startup the app loads:

1. `appsettings.json`
2. `appsettings.{Environment}.json` (if present)
3. Environment variable overrides with prefix `RP__`

### Environment variable override format

Use `RP__Section__Property=value`.

Examples:

- `RP__DataWarehouse__ConnectionString=Server=...`
- `RP__Backtest__InitialCapital=75000`
- `RP__DataIngestion__Universes=SP500,SP100`
- `RP__DataIngestion__Provider=Massive`
- `RP__DataIngestion__MassiveApiKey=your_api_key`
- `RP__DataIngestion__MassiveUseFixtureFallbackWhenApiKeyMissing=false`

Comma-separated values are parsed as arrays.

## Validation

Run:

```bash
./scripts/validate-config.sh
```

Runtime validation can skip rebuild when already compiled:

```bash
NO_BUILD=1 ./scripts/validate-config.sh
```

Fast JSON-only validation (no build/run):

```bash
./scripts/validate-config-json.sh
```

Or validate one environment:

```bash
RP_ENVIRONMENT=Test dotnet run --project src/Composition/ResearchPlatform.App/ResearchPlatform.App.csproj -- --validate-config
```
