using Rahoon.Api.Modules.Administration;
using Rahoon.Api.Modules.Agreements;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Complaints;
using Rahoon.Api.Modules.Owner;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Ecosystem;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;
using Rahoon.Api.Modules.Requests;
using Rahoon.Api.Modules.Sale;
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
        services.AddScoped<OfferExpiryMonitor>();
        services.AddHostedService<BreachMonitorService>();
        services.AddScoped<ProviderAssignmentService>();
        services.AddScoped<StaffInvitationService>();
        services.AddScoped<TemplateService>();
        services.AddScoped<PlatformMinima>();
        services.AddScoped<TempAccessService>();
        services.AddHostedService<TempAccessExpiryService>();
        services.AddScoped<SaleService>();
        services.AddScoped<ProviderDirectory>();
        services.AddScoped<ConditionalIntegrations>();
        services.AddScoped<RequestService>();
        services.AddScoped<RequestAccess>();
        services.AddScoped<RequestWorkflow>();
        return services;
    }

    public static IEndpointRouteBuilder MapRahoonEndpoints(this IEndpointRouteBuilder app)
    {
        AuthEndpoints.Map(app);
        IndividualAuthEndpoints.Map(app);
        MyRequestEndpoints.Map(app);
        TeamRequestEndpoints.Map(app);
        OfferEndpoints.Map(app);
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
        app.MapCaseTabs();
        AssignmentEndpoints.Map(app);
        ProviderPortalEndpoints.Map(app);
        StaffInvitationEndpoints.Map(app);
        OrganizationSettingsEndpoints.Map(app);
        DocumentRuleEndpoints.Map(app);
        ApprovalLimitEndpoints.Map(app);
        TemplateEndpoints.Map(app);
        InstitutionAdminEndpoints.Map(app);
        InstitutionApplicationEndpoints.Map(app);
        TempAccessEndpoints.Map(app);
        PlatformEndpoints.Map(app);
        SaleEndpoints.Map(app);
        SaleApprovalEndpoints.Map(app);
        BrokerSaleEndpoints.Map(app);
        OwnerSaleEndpoints.Map(app);
        ProviderOnboardingEndpoints.Map(app);
        PlatformProviderEndpoints.Map(app);
        InstitutionProviderEndpoints.Map(app);
        ProviderInvoiceEndpoints.Map(app);
        WorkflowDesignerEndpoints.Map(app);
        ReportingEndpoints.Map(app);
        BillingEndpoints.Map(app);
        ConditionalIntegrationEndpoints.Map(app);
        return app;
    }
}
