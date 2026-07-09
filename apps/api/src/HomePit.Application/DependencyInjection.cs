using HomePit.Application.Auth;
using HomePit.Application.Common;
using HomePit.Application.Finance;
using HomePit.Application.Gsm;
using HomePit.Application.Households;
using HomePit.Application.Institutional;
using HomePit.Application.Plans;
using HomePit.Application.Prompts;
using HomePit.Application.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace HomePit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddHomePitApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<HomePitDataPurgeService>();
        services.AddScoped<FinanceService>();
        services.AddScoped<GsmNumberService>();
        services.AddScoped<HouseholdService>();
        services.AddScoped<InstitutionalPageService>();
        services.AddScoped<CommercialPlanService>();
        services.AddScoped<ManagedImageQuotaService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<PromptService>();

        return services;
    }
}
