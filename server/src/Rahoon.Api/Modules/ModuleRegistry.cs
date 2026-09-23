using Rahoon.Api.Modules.Agreements;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Complaints;
using Rahoon.Api.Modules.Owner;
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
        services.AddScoped<OfferService>();
        services.AddScoped<AgreementService>();
        services.AddScoped<ComplaintService>();
        services.AddScoped<BreachMonitor>();
        services.AddHostedService<BreachMonitorService>();
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
        ApprovalEndpoints.Map(app);
        AgreementEndpoints.Map(app);
        ComplaintEndpoints.Map(app);
        OwnerEndpoints.Map(app);
        WorkspaceEndpoints.Map(app);
        return app;
    }
}
