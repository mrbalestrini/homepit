using System.IO;
using Xunit;

namespace HomePit.IntegrationTests;

public sealed class ApiContractSmokeTests
{
    [Fact]
    public void Openapi_contract_lists_superadmin_as_a_valid_system_role()
    {
        var contractPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "contracts", "openapi", "homepit.v1.yaml"));
        var contract = File.ReadAllText(contractPath);

        Assert.Contains("enum: [User, Admin, SuperAdmin]", contract);
    }

    [Fact]
    public void Openapi_contract_lists_public_and_admin_institutional_routes()
    {
        var contractPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "contracts", "openapi", "homepit.v1.yaml"));
        var contract = File.ReadAllText(contractPath);

        Assert.Contains("/api/institutional-page:", contract);
        Assert.Contains("/api/admin/institutional-page:", contract);
        Assert.Contains("enum: [hero, highlight, seo]", contract);
    }

    [Fact]
    public void Openapi_contract_lists_gsm_routes_and_status_enum()
    {
        var contractPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "contracts", "openapi", "homepit.v1.yaml"));
        var contract = File.ReadAllText(contractPath);

        Assert.Contains("/api/gsm-numbers:", contract);
        Assert.Contains("/api/gsm-numbers/{id}/recharges:", contract);
        Assert.Contains("/api/gsm-numbers/{id}/recharges/{rechargeId}:", contract);
        Assert.Contains("enum: [Ativo, Inativo, Abandonado]", contract);
        Assert.Contains("enum: [PrePago, PosPago]", contract);
        Assert.Contains("monthlyCost:", contract);
        Assert.Contains("daysWithoutRecharge:", contract);
    }

    [Fact]
    public void Openapi_contract_lists_finance_routes_and_enums()
    {
        var contractPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "contracts", "openapi", "homepit.v1.yaml"));
        var contract = File.ReadAllText(contractPath);

        Assert.Contains("/api/finance/periods:", contract);
        Assert.Contains("/api/finance/periods/{year}/{month}:", contract);
        Assert.Contains("/api/finance/periods/{year}/{month}/generate:", contract);
        Assert.Contains("/api/finance/recurring-templates:", contract);
        Assert.Contains("/api/finance/entries:", contract);
        Assert.Contains("/api/finance/assets:", contract);
        Assert.Contains("/api/finance/credit-cards:", contract);
        Assert.Contains("enum: [Entrada, Saida]", contract);
        Assert.Contains("enum: [Manual, RecurringTemplate, CreditCardStatement]", contract);
        Assert.Contains("enum: [Monthly, Annual]", contract);
        Assert.Contains("enum: [Property, Vehicle, Other]", contract);
    }
}
