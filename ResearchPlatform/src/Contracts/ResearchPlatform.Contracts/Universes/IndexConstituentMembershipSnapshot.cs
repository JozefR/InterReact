namespace ResearchPlatform.Contracts.Universes;

public sealed record IndexConstituentMembershipSnapshot(
    string IndexCode,
    long SymbolMasterId,
    string CanonicalSymbol,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal? Weight,
    string Source);
