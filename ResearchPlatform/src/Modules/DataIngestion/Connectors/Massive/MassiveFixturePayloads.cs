namespace DataIngestion.Connectors.Massive;

internal static class MassiveFixturePayloads
{
    public static readonly IReadOnlyDictionary<string, string> DailyAggsBySymbol = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["AAPL"] = """
{
  "ticker": "AAPL",
  "queryCount": 5,
  "resultsCount": 5,
  "adjusted": false,
  "results": [
    { "o": 196.11, "h": 198.33, "l": 194.87, "c": 197.90, "v": 54820000, "vw": 197.07, "date": "2026-02-10" },
    { "o": 197.85, "h": 199.42, "l": 196.73, "c": 198.94, "v": 50170000, "vw": 198.22, "date": "2026-02-11" },
    { "o": 199.01, "h": 200.12, "l": 197.55, "c": 198.11, "v": 47940000, "vw": 198.54, "date": "2026-02-12" },
    { "o": 198.07, "h": 201.20, "l": 197.89, "c": 200.88, "v": 52210000, "vw": 200.03, "date": "2026-02-13" },
    { "o": 201.03, "h": 202.74, "l": 199.86, "c": 201.56, "v": 49720000, "vw": 201.40, "date": "2026-02-17" }
  ],
  "status": "OK",
  "request_id": "fixture-aapl"
}
""",
        ["MSFT"] = """
{
  "ticker": "MSFT",
  "queryCount": 5,
  "resultsCount": 5,
  "adjusted": false,
  "results": [
    { "o": 431.42, "h": 434.11, "l": 428.90, "c": 433.35, "v": 26280000, "vw": 432.12, "date": "2026-02-10" },
    { "o": 433.10, "h": 436.45, "l": 432.55, "c": 435.80, "v": 24150000, "vw": 434.90, "date": "2026-02-11" },
    { "o": 435.95, "h": 437.32, "l": 433.20, "c": 434.08, "v": 23860000, "vw": 434.77, "date": "2026-02-12" },
    { "o": 434.15, "h": 438.70, "l": 433.80, "c": 437.92, "v": 25690000, "vw": 436.98, "date": "2026-02-13" },
    { "o": 438.10, "h": 439.54, "l": 436.66, "c": 438.88, "v": 22930000, "vw": 438.24, "date": "2026-02-17" }
  ],
  "status": "OK",
  "request_id": "fixture-msft"
}
""",
        ["NVDA"] = """
{
  "ticker": "NVDA",
  "queryCount": 5,
  "resultsCount": 5,
  "adjusted": false,
  "results": [
    { "o": 737.30, "h": 748.25, "l": 731.84, "c": 745.16, "v": 38110000, "vw": 742.39, "date": "2026-02-10" },
    { "o": 745.92, "h": 752.03, "l": 739.67, "c": 748.72, "v": 36650000, "vw": 747.01, "date": "2026-02-11" },
    { "o": 749.01, "h": 753.66, "l": 741.22, "c": 744.55, "v": 35980000, "vw": 746.11, "date": "2026-02-12" },
    { "o": 744.72, "h": 757.40, "l": 742.65, "c": 755.88, "v": 38990000, "vw": 752.55, "date": "2026-02-13" },
    { "o": 756.40, "h": 761.90, "l": 750.03, "c": 758.44, "v": 37420000, "vw": 757.08, "date": "2026-02-17" }
  ],
  "status": "OK",
  "request_id": "fixture-nvda"
}
""",
        ["AMZN"] = """
{
  "ticker": "AMZN",
  "queryCount": 5,
  "resultsCount": 5,
  "adjusted": false,
  "results": [
    { "o": 199.87, "h": 202.64, "l": 198.90, "c": 201.78, "v": 35670000, "vw": 201.06, "date": "2026-02-10" },
    { "o": 201.92, "h": 203.51, "l": 200.31, "c": 202.88, "v": 33440000, "vw": 202.24, "date": "2026-02-11" },
    { "o": 202.80, "h": 204.22, "l": 200.90, "c": 201.33, "v": 32710000, "vw": 201.81, "date": "2026-02-12" },
    { "o": 201.45, "h": 205.12, "l": 200.88, "c": 204.74, "v": 34960000, "vw": 203.92, "date": "2026-02-13" },
    { "o": 204.92, "h": 206.03, "l": 203.77, "c": 205.10, "v": 31830000, "vw": 204.96, "date": "2026-02-17" }
  ],
  "status": "OK",
  "request_id": "fixture-amzn"
}
"""
    };

    public static readonly string[] DefaultConstituentSymbols = ["AAPL", "MSFT", "NVDA", "AMZN"];

    public const string Dividends = """
{
  "request_id": "fixture-dividends",
  "results": [
    {
      "cash_amount": 0.81,
      "currency": "USD",
      "declaration_date": "2026-02-01",
      "distribution_type": "recurring",
      "ex_dividend_date": "2026-02-19",
      "frequency": 4,
      "historical_adjustment_factor": 0.998200,
      "id": "MSFT-20260219-DIV",
      "pay_date": "2026-03-12",
      "record_date": "2026-02-20",
      "split_adjusted_cash_amount": 0.81,
      "ticker": "MSFT"
    },
    {
      "cash_amount": 1.25,
      "currency": "USD",
      "declaration_date": "2026-02-12",
      "distribution_type": "special",
      "ex_dividend_date": "2026-02-24",
      "frequency": 0,
      "historical_adjustment_factor": 0.994700,
      "id": "AMZN-20260224-SPDIV",
      "pay_date": "2026-03-03",
      "record_date": "2026-02-25",
      "split_adjusted_cash_amount": 1.25,
      "ticker": "AMZN"
    }
  ],
  "status": "OK"
}
""";

    public const string Splits = """
{
  "request_id": "fixture-splits",
  "results": [
    {
      "adjustment_type": "forward_split",
      "execution_date": "2026-02-16",
      "historical_adjustment_factor": 0.500000,
      "id": "AAPL-20260216-SPLIT",
      "split_from": 1,
      "split_to": 2,
      "ticker": "AAPL"
    }
  ],
  "status": "OK"
}
""";
}
