namespace HomePit.Domain.Finance;

public static class FinanceCategoryCatalog
{
    public static readonly IReadOnlyList<string> DefaultNames =
    [
        "Salário",
        "Casa",
        "Mercado",
        "Refeição",
        "Saúde",
        "Filhos",
        "Carro",
        "Locomoção",
        "Investimentos",
        "Igreja",
        "Lazer",
        "Compras não essenciais"
    ];

    public static IReadOnlyCollection<FinanceCategory> CreateDefaults(Guid householdId, Guid? createdByMemberId)
    {
        return DefaultNames
            .Select((name, index) => new FinanceCategory
            {
                HouseholdId = householdId,
                CreatedByMemberId = createdByMemberId,
                Name = name,
                IsDefault = true,
                SortOrder = index
            })
            .ToArray();
    }
}
