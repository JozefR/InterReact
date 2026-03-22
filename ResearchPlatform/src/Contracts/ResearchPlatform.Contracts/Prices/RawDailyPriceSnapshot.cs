namespace ResearchPlatform.Contracts.Prices;

public sealed record RawDailyPriceSnapshot(
    long Id,
    long SymbolMasterId,
    string CanonicalSymbol,
    DateOnly TradeDate,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    decimal? Vwap,
    string Provider,
    Guid? LastIngestionRunId);
