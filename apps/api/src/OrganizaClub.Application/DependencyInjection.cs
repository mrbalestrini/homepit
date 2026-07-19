using OrganizaClub.Application.Auth;
using OrganizaClub.Application.Common;
using OrganizaClub.Application.Finance;
using OrganizaClub.Application.Gsm;
using OrganizaClub.Application.Spaces;
using OrganizaClub.Application.Institutional;
using OrganizaClub.Application.Integrations;
using OrganizaClub.Application.Platform;
using OrganizaClub.Application.Plans;
using OrganizaClub.Application.Prompts;
using OrganizaClub.Application.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace OrganizaClub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrganizaClubApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<OrganizaClubDataPurgeService>();
        services.AddScoped<FinanceService>();
        services.AddScoped<GsmNumberService>();
        services.AddScoped<SpaceService>();
        services.AddScoped<InstitutionalPageService>();
        services.AddScoped<IntegrationConnectionService>();
        services.AddScoped<IntegrationIdempotencyService>();
        services.AddScoped<PlatformSettingsService>();
        services.AddScoped<ToolImprovementSuggestionService>();
        services.AddScoped<CommercialPlanService>();
        services.AddScoped<ManagedImageQuotaService>();
        services.AddScoped<EffortPlanningService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<PromptService>();

        return services;
    }
}
