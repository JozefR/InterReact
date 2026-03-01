# Module Boundaries

This document defines hard boundaries for `ResearchPlatform` modules.

## Dependency Rule

- Modules under `src/Modules/*` may reference only `src/Contracts/ResearchPlatform.Contracts`.
- Modules may not reference other modules directly.
- The composition root (`src/Composition/ResearchPlatform.App`) wires modules together.

## Modules

- `DataIngestion`: provider adapters and ingestion workflows.
- `DataWarehouse`: persistence and curated research data.
- `BacktestEngine`: simulation and accounting.
- `StrategyRegistry`: strategy contracts and discovery.
- `ExperimentRunner`: experiment orchestration.
- `MetricsReporting`: metrics and report generation.
- `AiResearchAssistant`: AI planning and summarization over artifacts.

## Why this boundary matters

- Keeps modules independently testable and replaceable.
- Prevents accidental coupling between research concerns.
- Maintains clear direction: contracts inward, composition outward.
