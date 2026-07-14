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
            MaxUniverses: 3,
            MaxProjects: 3,
            MaxInvitedMembers: null,
            MaxOriginalImages: 30,
            ShowInCatalog: true,
            IsPopular: false,
            SortOrder: 0),
        new(
            StandardSlug,
            "Standard",
            DefaultCurrencyCode,
            MonthlyPrice: 9.90m,
            AnnualPrice: 99.00m,
            MaxOwnedHouseholds: 1,
            MaxUniverses: 3,
            MaxProjects: 3,
            MaxInvitedMembers: null,
            MaxOriginalImages: 30,
            ShowInCatalog: true,
            IsPopular: false,
            SortOrder: 1),
        new(
            BronzeSlug,
            "Bronze",
            DefaultCurrencyCode,
            MonthlyPrice: 19.90m,
            AnnualPrice: 199.00m,
            MaxOwnedHouseholds: 3,
            MaxUniverses: 6,
            MaxProjects: 6,
            MaxInvitedMembers: null,
            MaxOriginalImages: 50,
            ShowInCatalog: true,
            IsPopular: false,
            SortOrder: 2),
        new(
            SilverSlug,
            "Silver",
            DefaultCurrencyCode,
            MonthlyPrice: 29.90m,
            AnnualPrice: 299.00m,
            MaxOwnedHouseholds: 5,
            MaxUniverses: 9,
            MaxProjects: 9,
            MaxInvitedMembers: null,
            MaxOriginalImages: 100,
            ShowInCatalog: true,
            IsPopular: false,
            SortOrder: 3),
        new(
            GoldSlug,
            "Gold",
            DefaultCurrencyCode,
            MonthlyPrice: 39.90m,
            AnnualPrice: 399.00m,
            MaxOwnedHouseholds: 7,
            MaxUniverses: 15,
            MaxProjects: 15,
            MaxInvitedMembers: null,
            MaxOriginalImages: 300,
            ShowInCatalog: true,
            IsPopular: true,
            SortOrder: 4)
    ];

    public sealed record PlanDefinitionSeed(
        string Slug,
        string Name,
        string CurrencyCode,
        decimal MonthlyPrice,
        decimal AnnualPrice,
        int MaxOwnedHouseholds,
        int MaxUniverses,
        int MaxProjects,
        int? MaxInvitedMembers,
        int MaxOriginalImages,
        bool ShowInCatalog,
        bool IsPopular,
        int SortOrder);
}
