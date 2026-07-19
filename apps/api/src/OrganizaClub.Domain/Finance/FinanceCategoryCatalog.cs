namespace OrganizaClub.Domain.Finance;

public static class FinanceCategoryCatalog
{
    public static readonly IReadOnlyList<string> DefaultNames =
    [
        "Salário",
        "Espaço",
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

    public static IReadOnlyCollection<FinanceCategory> CreateDefaults(Guid spaceId, Guid? createdByMemberId)
    {
        return DefaultNames
            .Select((name, index) => new FinanceCategory
            {
                SpaceId = spaceId,
                CreatedByMemberId = createdByMemberId,
                Name = name,
                IsDefault = true,
                SortOrder = index
            })
            .ToArray();
    }
}
