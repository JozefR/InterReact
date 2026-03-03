namespace ResearchPlatform.Contracts.Universes;

public sealed record IndexConstituentInput(
    string CanonicalSymbol,
    decimal? Weight = null);
