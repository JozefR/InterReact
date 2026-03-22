namespace ResearchPlatform.Contracts.Prices;

public sealed record AdjustedDailyPriceSnapshot(
    long Id,
    long SymbolMasterId,
    string CanonicalSymbol,
    DateOnly TradeDate,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal AdjustedClose,
    decimal AdjustmentFactor,
    long Volume,
    string AdjustmentBasis,
    string Provider,
    Guid? LastIngestionRunId);
