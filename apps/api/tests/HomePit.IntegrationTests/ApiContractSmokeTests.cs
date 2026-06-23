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
}
