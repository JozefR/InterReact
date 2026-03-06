namespace ResearchPlatform.Contracts.Ingestion;

public sealed record ProviderDailyPriceRecord(
    string ProviderSymbol,
    DateOnly TradeDate,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    decimal? Vwap = null,
    string Currency = "USD");
