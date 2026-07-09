namespace HomePit.Domain.Plans;

public static class PlanDefinitionCatalog
{
    public const string FreeSlug = "free";
    public const string StandardSlug = "standard";
    public const string BronzeSlug = "bronze";
    public const string SilverSlug = "silver";
    public const string GoldSlug = "gold";
    public const string DefaultCurrencyCode = "BRL";

    public static IReadOnlyList<PlanDefinitionSeed> Defaults { get; } =
    [
        new(
            FreeSlug,
            "Free",
            DefaultCurrencyCode,
            MonthlyPrice: 0m,
            AnnualPrice: 0m,
            MaxOwnedHouseholds: 0,
            MaxUniversesPerHousehold: 3,
            MaxProjectsPerUniverse: 3,
            MaxOriginalImages: 30,
            SortOrder: 0),
        new(
            StandardSlug,
            "Standard",
            DefaultCurrencyCode,
            MonthlyPrice: 9.90m,
            AnnualPrice: 99.00m,
            MaxOwnedHouseholds: 1,
            MaxUniversesPerHousehold: 3,
            MaxProjectsPerUniverse: 3,
            MaxOriginalImages: 30,
            SortOrder: 1),
        new(
            BronzeSlug,
            "Bronze",
            DefaultCurrencyCode,
            MonthlyPrice: 19.90m,
            AnnualPrice: 199.00m,
            MaxOwnedHouseholds: 3,
            MaxUniversesPerHousehold: 6,
            MaxProjectsPerUniverse: 6,
            MaxOriginalImages: 50,
            SortOrder: 2),
        new(
            SilverSlug,
            "Silver",
            DefaultCurrencyCode,
            MonthlyPrice: 29.90m,
            AnnualPrice: 299.00m,
            MaxOwnedHouseholds: 5,
            MaxUniversesPerHousehold: 9,
            MaxProjectsPerUniverse: 9,
            MaxOriginalImages: 100,
            SortOrder: 3),
        new(
            GoldSlug,
            "Gold",
            DefaultCurrencyCode,
            MonthlyPrice: 39.90m,
            AnnualPrice: 399.00m,
            MaxOwnedHouseholds: 7,
            MaxUniversesPerHousehold: 15,
            MaxProjectsPerUniverse: 15,
            MaxOriginalImages: 300,
            SortOrder: 4)
    ];

    public sealed record PlanDefinitionSeed(
        string Slug,
        string Name,
        string CurrencyCode,
        decimal MonthlyPrice,
        decimal AnnualPrice,
        int MaxOwnedHouseholds,
        int MaxUniversesPerHousehold,
        int MaxProjectsPerUniverse,
        int MaxOriginalImages,
        int SortOrder);
}
