using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules;

/// <summary>Single place where each module registers its services and endpoints.</summary>
public static class ModuleRegistry
{
    public static IServiceCollection AddRahoonModules(this IServiceCollection services)
    {
        services.AddScoped<OtpService>();
        services.AddScoped<CaseFactory>();
        services.AddScoped<CaseAccess>();
        services.AddScoped<NextActionBuilder>();
        services.AddScoped<Notifier>();
        return services;
    }

    public static IEndpointRouteBuilder MapRahoonEndpoints(this IEndpointRouteBuilder app)
    {
        AuthEndpoints.Map(app);
        CaseListEndpoints.Map(app);
        CaseDraftEndpoints.Map(app);
        CaseEndpoints.Map(app);
        SolutionEndpoints.Map(app);
        DocumentEndpoints.Map(app);
        return app;
    }
}
