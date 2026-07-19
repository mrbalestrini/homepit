using System.IO;
using Xunit;

namespace OrganizaClub.IntegrationTests;

public sealed class ApiContractSmokeTests
{
    [Fact]
    public void Openapi_contract_lists_superadmin_as_a_valid_system_role()
    {
        var contractPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "contracts", "openapi", "organiza-club.v1.yaml"));
        var contract = File.ReadAllText(contractPath);

        Assert.Contains("enum: [User, Admin, SuperAdmin]", contract);
    }

    [Fact]
    public void Openapi_contract_lists_public_and_admin_institutional_routes()
    {
        var contractPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "contracts", "openapi", "organiza-club.v1.yaml"));
        var contract = File.ReadAllText(contractPath);

        Assert.Contains("/api/institutional-page:", contract);
        Assert.Contains("/api/admin/institutional-page:", contract);
        Assert.Contains("enum: [hero, highlight, seo]", contract);
    }

    [Fact]
    public void Openapi_contract_lists_platform_settings_routes()
    {
        var contractPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "contracts", "openapi", "organiza-club.v1.yaml"));
        var contract = File.ReadAllText(contractPath);

        Assert.Contains("/api/platform-settings:", contract);
        Assert.Contains("/api/admin/platform/settings:", contract);
        Assert.Contains("PublicPlatformSettings:", contract);
        Assert.Contains("PlatformSettings:", contract);
    }

    [Fact]
    public void Openapi_contract_lists_gsm_routes_and_status_enum()
    {
        var contractPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "contracts", "openapi", "organiza-club.v1.yaml"));
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
        var contractPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "contracts", "openapi", "organiza-club.v1.yaml"));
        var contract = File.ReadAllText(contractPath);

        Assert.Contains("/api/finance/periods:", contract);
        Assert.Contains("/api/finance/periods/{year}/{month}:", contract);
        Assert.Contains("/api/finance/periods/{year}/{month}/generate:", contract);
        Assert.Contains("/api/finance/categories:", contract);
        Assert.Contains("/api/finance/recurring-templates:", contract);
        Assert.Contains("/api/finance/entries:", contract);
        Assert.Contains("/api/finance/assets:", contract);
        Assert.Contains("/api/finance/credit-cards:", contract);
        Assert.Contains("enum: [Entrada, Saida]", contract);
        Assert.Contains("enum: [Manual, RecurringTemplate, CreditCardStatement]", contract);
        Assert.Contains("enum: [Monthly, Annual]", contract);
        Assert.Contains("enum: [Property, Vehicle, Other]", contract);
    }

    [Fact]
    public void Openapi_contract_lists_account_lifecycle_and_admin_user_routes()
    {
        var contractPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "contracts", "openapi", "organiza-club.v1.yaml"));
        var contract = File.ReadAllText(contractPath);

        Assert.Contains("/api/users/me/reactivate:", contract);
        Assert.Contains("/api/admin/users:", contract);
        Assert.Contains("/api/admin/users/{id}/deactivate:", contract);
        Assert.Contains("/api/admin/users/{id}/reactivate:", contract);
        Assert.Contains("/api/users/me/plan/creations/{scope}:", contract);
        Assert.Contains("enum: [Active, PendingSelfDeletion, DisabledBySuperAdmin]", contract);
        Assert.Contains("scheduledDeletionAt:", contract);
        Assert.Contains("spaceName:", contract);
    }

    [Fact]
    public void Openapi_contract_lists_effort_and_relevance_routes()
    {
        var contractPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "..", "contracts", "openapi", "organiza-club.v1.yaml"));
        var contract = File.ReadAllText(contractPath);

        Assert.Contains("/api/effort-plan:", contract);
        Assert.Contains("/api/activities/relevance:", contract);
        Assert.Contains("EffortPlan:", contract);
        Assert.Contains("ActivityRelevanceResponse:", contract);
        Assert.Contains("enum: [Scheduled, Overflow, MissingEstimate]", contract);
    }
}
