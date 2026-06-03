using HomePit.Application.Auth;
using HomePit.Application.Households;
using HomePit.Application.Prompts;
using HomePit.Application.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace HomePit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddHomePitApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<HouseholdService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<PromptService>();

        return services;
    }
}
