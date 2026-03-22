namespace ResearchPlatform.Contracts.Prices;

public static class AdjustmentBasisCodes
{
    public const string SplitOnly = "SplitOnly";
    public const string SplitAndDividend = "SplitAndDividend";

    public static readonly string[] Supported = [SplitOnly, SplitAndDividend];
}
