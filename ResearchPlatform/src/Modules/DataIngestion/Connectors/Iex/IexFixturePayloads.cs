namespace DataIngestion.Connectors.Iex;

internal static class IexFixturePayloads
{
    public const string Sp500ConstituentsJson = """
[
  { "symbol": "AAPL", "companyName": "Apple Inc.", "weight": 0.062, "primaryExchange": "NASDAQ" },
  { "symbol": "MSFT", "companyName": "Microsoft Corporation", "weight": 0.071, "primaryExchange": "NASDAQ" },
  { "symbol": "NVDA", "companyName": "NVIDIA Corporation", "weight": 0.053, "primaryExchange": "NASDAQ" },
  { "symbol": "AMZN", "companyName": "Amazon.com, Inc.", "weight": 0.041, "primaryExchange": "NASDAQ" },
  { "symbol": "GOOGL", "companyName": "Alphabet Inc. Class A", "weight": 0.039, "primaryExchange": "NASDAQ" }
]
""";

    public const string Sp100ConstituentsJson = """
[
  { "symbol": "AAPL", "companyName": "Apple Inc.", "weight": 0.101, "primaryExchange": "NASDAQ" },
  { "symbol": "MSFT", "companyName": "Microsoft Corporation", "weight": 0.107, "primaryExchange": "NASDAQ" },
  { "symbol": "AMZN", "companyName": "Amazon.com, Inc.", "weight": 0.082, "primaryExchange": "NASDAQ" },
  { "symbol": "META", "companyName": "Meta Platforms, Inc.", "weight": 0.074, "primaryExchange": "NASDAQ" }
]
""";

    public const string DailyPricesJson = """
[
  { "symbol": "AAPL", "date": "2026-02-10", "open": 196.11, "high": 198.33, "low": 194.87, "close": 197.90, "volume": 54820000, "vwap": 197.07, "currency": "USD" },
  { "symbol": "AAPL", "date": "2026-02-11", "open": 197.85, "high": 199.42, "low": 196.73, "close": 198.94, "volume": 50170000, "vwap": 198.22, "currency": "USD" },
  { "symbol": "AAPL", "date": "2026-02-12", "open": 199.01, "high": 200.12, "low": 197.55, "close": 198.11, "volume": 47940000, "vwap": 198.54, "currency": "USD" },
  { "symbol": "AAPL", "date": "2026-02-13", "open": 198.07, "high": 201.20, "low": 197.89, "close": 200.88, "volume": 52210000, "vwap": 200.03, "currency": "USD" },
  { "symbol": "AAPL", "date": "2026-02-17", "open": 201.03, "high": 202.74, "low": 199.86, "close": 201.56, "volume": 49720000, "vwap": 201.40, "currency": "USD" },
  { "symbol": "MSFT", "date": "2026-02-10", "open": 431.42, "high": 434.11, "low": 428.90, "close": 433.35, "volume": 26280000, "vwap": 432.12, "currency": "USD" },
  { "symbol": "MSFT", "date": "2026-02-11", "open": 433.10, "high": 436.45, "low": 432.55, "close": 435.80, "volume": 24150000, "vwap": 434.90, "currency": "USD" },
  { "symbol": "MSFT", "date": "2026-02-12", "open": 435.95, "high": 437.32, "low": 433.20, "close": 434.08, "volume": 23860000, "vwap": 434.77, "currency": "USD" },
  { "symbol": "MSFT", "date": "2026-02-13", "open": 434.15, "high": 438.70, "low": 433.80, "close": 437.92, "volume": 25690000, "vwap": 436.98, "currency": "USD" },
  { "symbol": "MSFT", "date": "2026-02-17", "open": 438.10, "high": 439.54, "low": 436.66, "close": 438.88, "volume": 22930000, "vwap": 438.24, "currency": "USD" },
  { "symbol": "NVDA", "date": "2026-02-10", "open": 737.30, "high": 748.25, "low": 731.84, "close": 745.16, "volume": 38110000, "vwap": 742.39, "currency": "USD" },
  { "symbol": "NVDA", "date": "2026-02-11", "open": 745.92, "high": 752.03, "low": 739.67, "close": 748.72, "volume": 36650000, "vwap": 747.01, "currency": "USD" },
  { "symbol": "NVDA", "date": "2026-02-12", "open": 749.01, "high": 753.66, "low": 741.22, "close": 744.55, "volume": 35980000, "vwap": 746.11, "currency": "USD" },
  { "symbol": "NVDA", "date": "2026-02-13", "open": 744.72, "high": 757.40, "low": 742.65, "close": 755.88, "volume": 38990000, "vwap": 752.55, "currency": "USD" },
  { "symbol": "NVDA", "date": "2026-02-17", "open": 756.40, "high": 761.90, "low": 750.03, "close": 758.44, "volume": 37420000, "vwap": 757.08, "currency": "USD" },
  { "symbol": "AMZN", "date": "2026-02-10", "open": 199.87, "high": 202.64, "low": 198.90, "close": 201.78, "volume": 35670000, "vwap": 201.06, "currency": "USD" },
  { "symbol": "AMZN", "date": "2026-02-11", "open": 201.92, "high": 203.51, "low": 200.31, "close": 202.88, "volume": 33440000, "vwap": 202.24, "currency": "USD" },
  { "symbol": "AMZN", "date": "2026-02-12", "open": 202.80, "high": 204.22, "low": 200.90, "close": 201.33, "volume": 32710000, "vwap": 201.81, "currency": "USD" },
  { "symbol": "AMZN", "date": "2026-02-13", "open": 201.45, "high": 205.12, "low": 200.88, "close": 204.74, "volume": 34960000, "vwap": 203.92, "currency": "USD" },
  { "symbol": "AMZN", "date": "2026-02-17", "open": 204.92, "high": 206.03, "low": 203.77, "close": 205.10, "volume": 31830000, "vwap": 204.96, "currency": "USD" }
]
""";

    public const string CorporateActionsJson = """
[
  { "symbol": "AAPL", "exDate": "2026-02-17", "type": "dividend", "amount": 0.25, "currency": "USD", "refId": "AAPL-DIV-20260217", "description": "Quarterly dividend", "relatedSymbol": null },
  { "symbol": "MSFT", "exDate": "2026-02-19", "type": "dividend", "amount": 0.78, "currency": "USD", "refId": "MSFT-DIV-20260219", "description": "Quarterly dividend", "relatedSymbol": null },
  { "symbol": "NVDA", "exDate": "2026-02-20", "type": "split", "amount": 2.0, "currency": null, "refId": "NVDA-SPLIT-20260220", "description": "2-for-1 stock split", "relatedSymbol": null },
  { "symbol": "AMZN", "exDate": "2026-02-24", "type": "special_dividend", "amount": 1.25, "currency": "USD", "refId": "AMZN-SPDIV-20260224", "description": "Special distribution", "relatedSymbol": null }
]
""";
}
